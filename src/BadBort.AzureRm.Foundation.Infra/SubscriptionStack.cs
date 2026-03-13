using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;
using Pulumi;
using Pulumi.Azure.ArmMsi;
using Pulumi.Azure.Authorization;
using Pulumi.Azure.Core;
using Pulumi.Azure.Inputs;
using Pulumi.AzureAD;
using Pulumi.AzureNative.Consumption;
using Pulumi.AzureNative.Consumption.Inputs;
using Config = Pulumi.Config;
using NativeProvider = Pulumi.AzureNative.Provider;
using NativeProviderArgs = Pulumi.AzureNative.ProviderArgs;
using Provider = Pulumi.Azure.Provider;
using ProviderArgs = Pulumi.Azure.ProviderArgs;

namespace BadBort.AzureRm.Foundation.Infra;

/// <summary>
/// Manages resource groups, user assigned identities and role assignments within a tenant across multiple subscriptions.
/// </summary>
public class SubscriptionStack : Stack
{
    private readonly Dictionary<string, UserAssignedIdentity> _uamiLookup = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<RgContext, ResourceGroup> _resourceGroups = new();
    private readonly Dictionary<string, string> _tenantUserAliases = new(StringComparer.InvariantCultureIgnoreCase);

    public SubscriptionStack()
    {
        var config = new Config();

        string dataDir = config.Require(ConfigKeys.YamlRoot);
        string tenantConfig = config.Require(ConfigKeys.Tenant);

        Log.Info($"Loading data directory: {dataDir}. Full path: {Path.GetFullPath(dataDir)}");

        var dirContext = new FileSystemConvention(dataDir);

        var tenantInfo = dirContext.GetTenants().FirstOrDefault(t => t.Id == tenantConfig || t.Alias == tenantConfig);

        if (tenantInfo == null)
        {
            throw new InvalidOperationException("Could not find tenant in data_dir");
        }
        
        foreach (var (alias, identifier) in tenantInfo.Config.Tenant.UserAliases ?? new())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alias);
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            _tenantUserAliases[alias] = identifier;
        }

        var subscriptions = dirContext.GetSubscriptions(tenantInfo);
        Log.Info($"Executing for tenant {tenantInfo.Id} with alias {tenantInfo.Alias}. Located {subscriptions.Count} subscriptions");

        var subscriptionProviders = subscriptions.ToDictionary(s => s, s => new Provider($"az-{s.Id}", new ProviderArgs
        {
            SubscriptionId = s.Id,
            UseOidc = true,
            Features = new ProviderFeaturesArgs
            {
                ResourceGroup = new ProviderFeaturesResourceGroupArgs
                {
                    PreventDeletionIfContainsResources = true
                }
            }
        }));
        var nativeProviders = subscriptions.ToDictionary(s => s, s => new NativeProvider($"azn-{s.Id}", new NativeProviderArgs
        {
            SubscriptionId = s.Id,
            TenantId = tenantInfo.Id,
            UseOidc = true
        }));

        // Resource groups and UAMIs first
        foreach (SubscriptionInfo subscriptionInfo in subscriptions)
        {
            Subscription(subscriptionInfo, subscriptionProviders[subscriptionInfo], nativeProviders[subscriptionInfo]);
        }

        // Role assignments after all identities have been declared
        foreach (SubscriptionInfo subscriptionInfo in subscriptions)
        {
            SubscriptionRoleAssignments(subscriptionInfo, subscriptionProviders[subscriptionInfo]);
        }
    }

    private void Subscription(SubscriptionInfo subscriptionInfo, Provider azureProvider, NativeProvider nativeProvider)
    {
        Log.Info($"Executing for subscription {subscriptionInfo.Id} with alias {subscriptionInfo.Alias}");

        foreach (var resourceInfo in subscriptionInfo.Resources)
        {
            foreach (var (resourceGroupName, resourceGroupConfig) in resourceInfo.Config.ResourceGroups ?? new())
            {
                var id = $"{subscriptionInfo.Id}-{resourceGroupName}";
                
                var rg = new ResourceGroup(id, new ResourceGroupArgs
                {
                    Name = resourceGroupName,
                    Location = resourceGroupConfig.Location!,
                    Tags = resourceGroupConfig.Tags ?? new InputMap<string>() // if your model includes Tags
                }, new CustomResourceOptions
                {
                    Provider = azureProvider,
                    Parent = azureProvider,
                });

                _resourceGroups[new(subscriptionInfo, resourceGroupName)] = rg;

                CreateBudgets(subscriptionInfo, resourceGroupName, resourceGroupConfig, rg, nativeProvider);

                foreach (var userAssignedIdentity in resourceGroupConfig.UserAssignedIdentities ?? new())
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(userAssignedIdentity.Name);

                    var uami = new UserAssignedIdentity(userAssignedIdentity.Name, new UserAssignedIdentityArgs
                    {
                        ResourceGroupName = rg.Name,
                        Name = userAssignedIdentity.Name,
                        Location = rg.Location,
                        Tags = resourceGroupConfig.Tags ?? new InputMap<string>()
                    }, new CustomResourceOptions
                    {
                        Parent = rg
                    });

                    Log.Info("Creating user assigned identity: " + userAssignedIdentity.Name);
                    
                    _uamiLookup.Add(userAssignedIdentity.Name, uami);

                    foreach (var federatedCredentialRaw in userAssignedIdentity.FederatedCredentials ?? new())
                    {
                        var federatedCredential = federatedCredentialRaw.GetPopulatedInstance();

                        _ = new FederatedIdentityCredential($"{userAssignedIdentity.Name}-{federatedCredential.Name!}",
                            new FederatedIdentityCredentialArgs
                            {
                                ResourceGroupName = rg.Name,
                                ParentId = uami.Id,
                                Name = federatedCredential.Name!,
                                Issuer = federatedCredential.Issuer!,
                                Subject = federatedCredential.SubjectIdentifier!,
                                Audience = federatedCredential.Audience ?? "api://AzureADTokenExchange"
                            }, new CustomResourceOptions
                            {
                                Provider = azureProvider,
                                Parent = uami
                            });
                    }
                }
            }
        }
    }

    private void CreateBudgets(SubscriptionInfo subscriptionInfo, string resourceGroupName, ResourceGroupConfig resourceGroupConfig, ResourceGroup resourceGroup, NativeProvider nativeProvider)
    {
        foreach (ResourceGroupBudgetConfig budgetConfig in resourceGroupConfig.Budgets ?? new())
        {
            ValidateBudgetConfig(budgetConfig, resourceGroupName);

            var budgetName = budgetConfig.Name!;
            var timeGrain = string.IsNullOrWhiteSpace(budgetConfig.TimeGrain) ? "Monthly" : budgetConfig.TimeGrain!;

            var notifications = new InputMap<NotificationArgs>();
            foreach (BudgetNotificationConfig notificationConfig in budgetConfig.Notifications ?? new())
            {
                var notificationName = notificationConfig.Name!;
                var contactEmails = new InputList<string>();
                foreach (var email in notificationConfig.ContactEmails ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        contactEmails.Add(email);
                    }
                }
                foreach (var userReference in notificationConfig.ContactUsers ?? [])
                {
                    contactEmails.Add(ResolveBudgetContactUserEmail(userReference, resourceGroupName, budgetName, notificationName));
                }
                
                notifications[notificationName] = new NotificationArgs
                {
                    Enabled = notificationConfig.Enabled ?? true,
                    Operator = string.IsNullOrWhiteSpace(notificationConfig.Operator) ? "GreaterThan" : notificationConfig.Operator!,
                    Threshold = (double)notificationConfig.ThresholdPercent!.Value,
                    ContactEmails = contactEmails,
                    ContactGroups = notificationConfig.ContactGroups ?? []
                };
            }

            var timePeriod = new BudgetTimePeriodArgs
            {
                StartDate = budgetConfig.StartDate!
            };
            if (!string.IsNullOrWhiteSpace(budgetConfig.EndDate))
            {
                timePeriod.EndDate = budgetConfig.EndDate!;
            }

            _ = new Budget(
                $"budget-{subscriptionInfo.Alias}-{resourceGroupName}-{budgetName}".ToLowerInvariant(),
                new BudgetArgs
                {
                    BudgetName = budgetName,
                    Amount = (double)budgetConfig.Amount!.Value,
                    Category = "Cost",
                    Scope = resourceGroup.Id,
                    TimeGrain = timeGrain,
                    TimePeriod = timePeriod,
                    Notifications = notifications
                },
                new CustomResourceOptions
                {
                    Provider = nativeProvider,
                    Parent = resourceGroup
                });
        }
    }

    private static void ValidateBudgetConfig(ResourceGroupBudgetConfig budgetConfig, string resourceGroupName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetConfig.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetConfig.StartDate);

        if (budgetConfig.Amount is null || budgetConfig.Amount <= 0)
        {
            throw new InvalidOperationException($"Budget '{budgetConfig.Name}' in resource group '{resourceGroupName}' must have an amount greater than 0.");
        }

        if (!DateTimeOffset.TryParse(budgetConfig.StartDate, out _))
        {
            throw new InvalidOperationException($"Budget '{budgetConfig.Name}' in resource group '{resourceGroupName}' has an invalid start_date '{budgetConfig.StartDate}'.");
        }

        if (!string.IsNullOrWhiteSpace(budgetConfig.EndDate) && !DateTimeOffset.TryParse(budgetConfig.EndDate, out _))
        {
            throw new InvalidOperationException($"Budget '{budgetConfig.Name}' in resource group '{resourceGroupName}' has an invalid end_date '{budgetConfig.EndDate}'.");
        }

        foreach (var notification in budgetConfig.Notifications ?? [])
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(notification.Name);
            if (notification.ThresholdPercent is null || notification.ThresholdPercent <= 0)
            {
                throw new InvalidOperationException($"Budget notification '{notification.Name}' in resource group '{resourceGroupName}' must set threshold_percent greater than 0.");
            }

            if ((notification.ContactEmails?.Count ?? 0) == 0 && (notification.ContactGroups?.Count ?? 0) == 0)
            {
                if ((notification.ContactUsers?.Count ?? 0) == 0)
                {
                    throw new InvalidOperationException($"Budget notification '{notification.Name}' in resource group '{resourceGroupName}' must include contact_emails, contact_users, or contact_groups.");
                }
            }

            foreach (var userReference in notification.ContactUsers ?? [])
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(userReference);
            }
        }
    }

    private Input<string> ResolveBudgetContactUserEmail(string userReference, string resourceGroupName, string budgetName, string notificationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userReference);
        var resolvedIdentifier = ResolveUserIdentifier(userReference);
        var lookupArgs = BuildGetUserLookupArgs(resolvedIdentifier);
        var user = GetUser.Invoke(lookupArgs);

        return user.Apply(o =>
        {
            if (string.IsNullOrWhiteSpace(o.Mail))
            {
                throw new InvalidOperationException($"Resolved budget contact user '{userReference}' for '{resourceGroupName}/{budgetName}/{notificationName}' has no mail value in Entra.");
            }

            return o.Mail;
        });
    }

    private string ResolveUserIdentifier(string userReference)
    {
        if (_tenantUserAliases.TryGetValue(userReference, out var mappedIdentifier))
        {
            if (string.IsNullOrWhiteSpace(mappedIdentifier))
            {
                throw new InvalidOperationException($"Tenant user alias '{userReference}' resolved to an empty identifier.");
            }
            return mappedIdentifier;
        }

        return userReference;
    }

    private static GetUserInvokeArgs BuildGetUserLookupArgs(string resolvedIdentifier)
    {
        if (Guid.TryParse(resolvedIdentifier, out _))
        {
            return new GetUserInvokeArgs
            {
                ObjectId = resolvedIdentifier
            };
        }

        if (resolvedIdentifier.Contains('@'))
        {
            return new GetUserInvokeArgs
            {
                UserPrincipalName = resolvedIdentifier
            };
        }

        return new GetUserInvokeArgs
        {
            MailNickname = resolvedIdentifier
        };
    }

    /// <summary>
    /// Add all the role assignments within the subscription.
    /// </summary>
    private void SubscriptionRoleAssignments(SubscriptionInfo subscriptionInfo, Provider azureProvider)
    {
        foreach (SubscriptionResourcesInfo resourceInfo in subscriptionInfo.Resources)
        {
            foreach (var (resourceGroupName, resourceGroupConfig) in resourceInfo.Config.ResourceGroups ?? new())
            {
                // Resource group role assignments
                if (!_resourceGroups.TryGetValue(new RgContext(subscriptionInfo, resourceGroupName), out ResourceGroup? resourceGroup))
                {
                    throw new NullReferenceException("Could not find resource group");
                }

                foreach (RoleAssignment assignment in resourceGroupConfig.RoleAssignments ?? new())
                {
                    RoleAssignment(assignment, "rg-" + resourceGroupName, resourceGroup.Id, resourceGroup, azureProvider);
                }

                foreach (UserAssignedIdentifyConfig uamiConfig in resourceGroupConfig.UserAssignedIdentities ?? new())
                {
                    if (string.IsNullOrWhiteSpace(uamiConfig.Name))
                        continue;

                    var uamiResource = _uamiLookup[uamiConfig.Name];

                    foreach (var roleAssignment in uamiConfig.ManagedIdentityOperators ?? new())
                    {
                        var identityInfo = roleAssignment.GetIdentityInfo();

                        // Only support service principals for now
                        if (identityInfo == null || string.IsNullOrEmpty(roleAssignment.ServicePrincipal))
                        {
                            continue;
                        }

                        var principalId = GetServicePrincipalId(roleAssignment.ServicePrincipal);

                        _ = new Assignment(
                            name: $"ra-uami-{identityInfo.IdentityType}-{identityInfo.Name}-managed-identity-operator".ToLower(),
                            new AssignmentArgs
                            {
                                PrincipalId = principalId,
                                RoleDefinitionName = "Managed Identity Operator",
                                Scope = uamiResource.Id,
#pragma warning disable CS8604 // Possible null reference argument.
                                Description = roleAssignment.Description
#pragma warning restore CS8604 // Possible null reference argument.
                            },
                            new CustomResourceOptions
                            {
                                Provider = azureProvider,
                                Parent = uamiResource
                            });
                    }
                }
            }

            // Subscription level role assignments
            foreach (RoleAssignment assignment in resourceInfo.Config.RoleAssignments ?? new())
            {
                RoleAssignment(assignment, "subscription-" + subscriptionInfo.Alias, subscriptionInfo.Id, azureProvider, azureProvider);
            }
        }
    }

    private void RoleAssignment(RoleAssignment assignment, string scopeName, Input<string> scope, Resource? parentResource, Provider provider)
    {
        Output<string>? principalId = null;

        var identityInfo = assignment.GetIdentityInfo();

        if (identityInfo == null)
        {
            return;
        }

        if (assignment.ServicePrincipal != null)
        {
            principalId = GetServicePrincipalId(assignment.ServicePrincipal);
        }
        else if (assignment.Group != null)
        {
            var group = GetGroup.Invoke(new GetGroupInvokeArgs
            {
                DisplayName = assignment.Group
            });

            principalId = group.Apply(o => o.ObjectId);
        }

        if (principalId == null)
        {
            throw new InvalidOperationException("Could not find identity to perform role assignment");
        }

        foreach (string role in assignment.Roles ?? new())
        {
            _ = new Assignment(
                name: $"ra-{scopeName}-{identityInfo.IdentityType}-{identityInfo.Name}-{role.Replace(" ", "-")}".ToLower(),
                new AssignmentArgs
                {
                    PrincipalId = principalId,
                    RoleDefinitionName = role,
                    Scope = scope,
#pragma warning disable CS8604 // Possible null reference argument.
                    Description = assignment.Description
#pragma warning restore CS8604 // Possible null reference argument.
                },
                new CustomResourceOptions
                {
                    Provider = provider,
                    Parent = parentResource
                });
        }
    }

    private Output<string> GetServicePrincipalId(string servicePrincipalName)
    {
        if (_uamiLookup.TryGetValue(servicePrincipalName, out var userAssignedIdentity))
        {
            return userAssignedIdentity.PrincipalId;
        }

        var sp = GetServicePrincipal.Invoke(new GetServicePrincipalInvokeArgs
        {
            DisplayName = servicePrincipalName
        });

        return sp.Apply(o => o.ObjectId);
    }

    record RgContext(SubscriptionInfo SubscriptionAlias, string ResourceGroup);
}
