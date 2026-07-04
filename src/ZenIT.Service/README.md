# ZenIT.Service

Reserved for the future Windows service or privileged execution broker.

The first version intentionally ships only `ZenIT.App` and `ZenIT.Core`. Future service work should implement the contracts in `ZenIT.Core.Services` and execute signed scripts from `scripts/windows` without hardcoded administrator credentials.
