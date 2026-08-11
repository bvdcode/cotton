# 19. Search

Cotton provides layout-scoped search over the authenticated user's visible folders and files. Search covers normalized display names and identifiers; it does not inspect file contents and does not cross layout or user boundaries.

## HTTP contract

```http
GET /api/v1/layouts/{layoutId}/search?query={query}&page={page}&pageSize={pageSize}
```

- Authentication is required.
- `page` defaults to `1`.
- `pageSize` defaults to `20` and cannot exceed `100`.
- `X-Total-Count` contains the total number of matching items before paging.
- The response body is a `SearchResultDto`.

| Response field | Meaning |
| --- | --- |
| `nodes` | Matching folders in ranked order. |
| `files` | Matching files in ranked order. |
| `nodePaths` | Absolute layout paths keyed by folder ID. |
| `filePaths` | Absolute layout paths, including the filename, keyed by file ID. |

The total count is transport metadata in `X-Total-Count`; it is not duplicated in the JSON payload.

## Search semantics

- Names are compared through the same normalized name keys used by the logical filesystem. Matching is therefore case- and diacritic-insensitive.
- A single text token uses substring matching. Multiple tokens must all be present.
- GUIDs match folder IDs, file IDs, and file-manifest IDs.
- When a query contains a GUID, identifier matching takes precedence over accompanying text.
- Trashed items are excluded.
- Empty queries and invalid paging values are rejected.

Results use deterministic relevance tiers: exact identifier, exact normalized name, prefix, multi-token match, then substring match. Equal scores are ordered consistently so paging remains stable.

## Processing model

The controller sends a `SearchLayoutsQuery` through the mediator pipeline. Its handler:

1. validates paging and builds normalized search criteria;
2. asks eligible `ILayoutSearchProvider` implementations for database queries;
3. combines and deduplicates ranked hits;
4. counts and pages hits in the database;
5. loads the matching folder and file DTOs;
6. resolves paths for the returned page;
7. returns `PagedResult<SearchResultDto>` to the controller.

The controller writes `TotalCount` to the response header and returns only the payload as JSON.

Provider queries remain `IQueryable` until paging. This is a required performance and correctness invariant: filtering, deduplication, counting, ordering, and paging must stay SQL-translatable rather than materializing the complete result set in application memory.

## Provider extension point

`ILayoutSearchProvider` is the internal extension contract for additional search strategies. A provider declares whether it can serve the normalized criteria and contributes ranked hits without owning paging or response construction.

Name and identifier matching is the only functional strategy today. A reserved vector-search provider contributes no results, so Cotton does not currently provide semantic search.

When adding a provider:

- scope every query by authenticated user and layout;
- exclude trash unless the public search contract changes;
- return SQL-translatable projections;
- preserve deterministic scoring and ordering;
- rely on the common pipeline for deduplication, paging, DTO loading, and path resolution.

## Security and failure boundaries

- The user ID comes from the authenticated principal, never from request parameters.
- Provider queries enforce both owner and layout scope.
- Identifier search cannot reveal another user's entities because the same scope applies before matching.
- User text is escaped before it is used in SQL `LIKE` patterns.
- Path resolution is bounded against cycles and excessive depth.
- Missing path ancestry falls back to the layout root rather than escaping the requested layout.

## Performance notes

Exact and prefix name matches can benefit from name-key indexes. Substring matching begins with a wildcard and may scan the caller's scoped layout subset. Search therefore keeps counting and paging in the database and loads full DTOs only for the requested page.

The browser trims and debounces search input before sending a request. This reduces avoidable traffic but does not replace server-side validation or isolation.

## Related sections

See [Logical Filesystem](05-logical-filesystem.md), [HTTP API and Mediator Layer](12-http-api-mediator.md), and [Frontend Architecture](23-frontend-architecture.md).
