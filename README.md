# JASS File Organizer Pro — v2.1 Final

A Windows .NET 8 WPF file-intelligence and safe-organization application.

## Final feature set
- Recursive folder scanning with cancellation
- Search by filename/path
- Existing category filter: All, Images, Videos, Audio, Documents, PDF, Archives, Code, Data, Projects, Other
- Minimum/maximum size filtering (MB)
- Older-than-N-days filtering
- Sortable file grid
- Image preview and detailed inspector
- Created/modified/accessed dates and attributes
- Exact SHA-256 duplicate detection
- Conservative visual-similarity detection using image perceptual hashing
- File intelligence dashboard
- Batch rename with `{n}`, `{name}`, `{ext}` tokens
- Organization planner with preview before execution
- Type-based safe organization into subfolders
- Operation history
- CSV/JSON/HTML reporting inherited from v2.0 foundation
- Recycle Bin deletion rather than permanent deletion
- Windows default application opening
- Copy path

## Safety model
Organization is a two-step workflow: **Plan → Review → Apply**. The planner does not move files until the user explicitly confirms Apply Plan.

## Requirements
- Windows 10/11
- .NET 8 SDK or later

## Build
```powershell
dotnet build
dotnet run
```

## Notes
Visual similarity is intentionally conservative and is not a semantic AI image classifier. Exact duplicates are verified using SHA-256.
