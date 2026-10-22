# Repository routing

## Database mutations and integrity

- Bulk mutations that bypass change tracking are centralized in `src/Cotton.Server/Extensions/ApprovedBulkMutationExtensions.cs`.
- Before adding any member or changing an existing operation in that file, obtain two separate, explicit confirmations from the requesting user for that exact change.
- Every update must have a behavioral test proving that the updated fields are excluded from the actual integrity descriptor and that existing signatures remain valid.
- Deletion tests must prove the exact scope and that retained rows and signatures remain unchanged.
- Behavioral coverage is in `src/Cotton.Server.IntegrationTests/ApprovedBulkMutationExtensionsTests.cs` and related integrity tests. Source-file hashes are not a substitute for those checks.
- Metadata changes must use tracked entities and `SaveChangesAsync` so integrity signatures are updated with the data.
