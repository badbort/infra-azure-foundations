using BadBort.AzureRm.Foundation.Infra.Model;
using BadBort.AzureRm.Foundation.Infra.Serialization;
using Pulumi;
using Pulumi.Azure.ArmMsi;
using Pulumi.Azure.Authorization;
using Pulumi.Azure.Core;
using Pulumi.AzureAD;
using Config = Pulumi.Config;
using Provider = Pulumi.Azure.Provider;
using ProviderArgs = Pulumi.Azure.ProviderArgs;
using RoleAssignment = BadBort.AzureRm.Foundation.Infra.Model.RoleAssignment;

namespace BadBort.AzureRm.Foundation.Infra;

/// <summary>
/// Manages resource groups, user assigned identities and role assignments within a tenant across multiple subscriptions.
/// </summary>
public class SubscriptionStack : Stack
{
    private readonly Dictionary<string, UserAssignedIdentity> _uamiLookup = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly Dictionary<RgContext, ResourceGroup> _resourceGroups = new();

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

        var subscriptions = dirContext.GetSubscriptions(tenantInfo);
        Log.Info($"Executing for tenant {tenantInfo.Id} with alias {tenantInfo.Alias}. Located {subscriptions.Count} subscriptions");

        var subscriptionProviders = subscriptions.ToDictionary(s => s, s => new Provider($"az-{s.Id}", new ProviderArgs
        {
            SubscriptionId = s.Id,
            UseOidc = true,
        }));

        // Resource groups and UAMIs first
        foreach (SubscriptionInfo subscriptionInfo in subscriptions)
        {
            Subscription(subscriptionInfo, subscriptionProviders[subscriptionInfo]);
        }

        // Role assignments after all identities have been declared
        foreach (SubscriptionInfo subscriptionInfo in subscriptions)
        {
            SubscriptionRoleAssignments(subscriptionInfo, subscriptionProviders[subscriptionInfo]);
        }
    }

    private void Subscription(SubscriptionInfo subscriptionInfo, Provider azureProvider)
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
                    Parent = azureProvider
                });

                _resourceGroups[new(subscriptionInfo, resourceGroupName)] = rg;

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

                        _ = new FederatedIdentityCredential(federatedCredential.Name!,
                            new FederatedIdentityCredentialArgs
                            {
                                ResourceGroupName = rg.Name,
                                ParentId = uami.Id,
                                Name = uami.Name,
                                Issuer = federatedCredential.Issuer!,
                                Subject = federatedCredential.SubjectIdentifier!,
                                Audience = federatedCredential.Issuer ?? "api://AzureADTokenExchange"
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

                        var principalId = GetServicePrincipleId(roleAssignment.ServicePrincipal);

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

        if (assignment.ServicePrinciple != null)
        {
            principalId = GetServicePrincipleId(assignment.ServicePrinciple);
        }
        else if (assignment.Group != null)
        {
            var group = GetGroup.Invoke(new GetGroupInvokeArgs
            {
                DisplayName = assignment.Group
            });

            principalId = group.Apply(o => o.Id);
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

    private Output<string> GetServicePrincipleId(string servicePrincipalName)
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