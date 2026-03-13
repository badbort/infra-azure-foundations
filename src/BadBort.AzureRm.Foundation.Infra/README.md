# Introduction

This repo provisions Azure foundation resources from YAML definitions.

#### Subscriptions

- Resource Groups
- User Assigned Managed Identities
- Role Assignments scoped to resource groups
- Resource-group scoped budgets and cost notifications

## Resource group budgets

Optionally define tenant user aliases in `data/<tenant>/tenant.yaml`:

```yaml
tenant:
  subscription_aliases:
    main: <subscription-id>
  user_aliases:
    finops-lead: 11111111-2222-3333-4444-555555555555
    engineering-manager: eng.manager@contoso.example
```

Define budgets in subscription YAML files under `data/<tenant>/<subscription>/**/*.yaml`:

```yaml
resource_groups:
  rg-workloads:
    location: Australia East
    budgets:
      - name: rg-workloads-monthly
        amount: 500
        time_grain: Monthly
        start_date: 2026-01-01T00:00:00Z
        end_date: 2026-12-31T23:59:59Z
        notifications:
          - name: warn-80
            threshold_percent: 80
            contact_emails:
              - finops@example.com
            contact_users:
              - finops-lead
          - name: crit-100
            threshold_percent: 100
            contact_users:
              - engineering-manager
              - 11111111-2222-3333-4444-555555555555
            contact_groups:
              - /subscriptions/<sub-id>/resourceGroups/<rg>/providers/microsoft.insights/actionGroups/<ag-name>
```

Budget validation rules:

- `amount` must be greater than `0`
- `start_date` (and optional `end_date`) must be valid dates
- notification `threshold_percent` must be greater than `0`
- each notification must include at least one of `contact_emails`, `contact_users`, or `contact_groups`
- `contact_users` accepts a user object id (GUID), user principal name, or a tenant `user_aliases` key
