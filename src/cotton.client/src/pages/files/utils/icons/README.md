# Icon System Architecture

## Overview
Unified icon system for files and folders following SOLID principles.

## Structure
```
src/pages/files/utils/icons/
├── index.ts           # Public API exports
├── types.ts           # Shared type definitions
├── FolderIcon.tsx     # Folder icon implementation
└── FileIcon.tsx       # File icon implementation
```

## Design Principles

### Single Responsibility Principle (SRP)
- **FolderIcon.tsx**: Only handles folder icon rendering
- **FileIcon.tsx**: Only handles file icon rendering and type detection
- Each helper function has one clear purpose (extractExtension, truncateExtension, etc.)

### Open/Closed Principle (OCP)
- Easy to add new file types by extending `FILE_TYPE_CONFIGS`
- Can add new icon strategies without modifying existing code
- Theme-aware styling allows easy theme additions

### Liskov Substitution Principle (LSP)
- All icon functions return `IconResult` (string | ReactNode)
- Components can use any icon function interchangeably

### Interface Segregation Principle (ISP)
- `getFolderIcon()` - Simple, no parameters needed
- `getFileIcon(previewHash, fileName)` - File-specific parameters
- Clients only depend on what they need

### Dependency Inversion Principle (DIP)
- Components depend on `IconResult` abstraction, not concrete implementations
- Icon generation logic is isolated from UI components

## Usage

### For Folders
```tsx
import { getFolderIcon } from '@/utils/icons';

const icon = getFolderIcon();
```

### For Files
```tsx
import { getFileIcon } from '@/utils/icons';

const icon = getFileIcon(
  file.encryptedFilePreviewHashHex ?? null,
  file.name
);

// Returns either:
// - Preview image URL (string)
// - React icon component (ReactNode)
const previewUrl = typeof icon === "string" ? icon : null;
```

## Icon Rendering Strategies

### Files
1. **Server Preview**: If `previewHash` is provided, returns URL to webp image
2. **Image Files**: Special icon for image file types (jpg, png, etc.)
3. **Generic Files**: Document icon with extension label overlay

### Folders
- Consistent folder icon with theme-aware coloring

## Theming
- **Light mode**: Icons use subtle gray (`rgba(0, 0, 0, 0.26)`)
- **Dark mode**: Icons use default theme color
- Extension labels are always readable with proper contrast

## Constants
- `ICON_SIZE = 120`: Unified size for all file system icons
- `MAX_EXTENSION_LENGTH = 6`: Maximum characters shown on file icon

## Extensibility

### Adding New File Types
```tsx
// In FileIcon.tsx
const FILE_TYPE_CONFIGS = {
  image: ["jpg", "jpeg", "png", ...],
  video: ["mp4", "avi", "mov", ...],  // Add new type
  document: ["pdf", "doc", "docx", ...],
} as const;
```

### Adding Custom Icon Strategy
```tsx
// In getFileIcon function
if (isVideoExtension(extension)) {
  return getVideoFileIcon();
}
```

## Migration from Old System
- ❌ `getFilePreview()` - Removed
- ❌ `getItemIcon.tsx` - Removed
- ✅ `getFileIcon()` - Use this
- ✅ `getFolderIcon()` - Use this

## Benefits
1. **Consistency**: All icons use same size and theming
2. **Maintainability**: Clear separation of concerns
3. **Testability**: Each function can be tested independently
4. **Extensibility**: Easy to add new file types and icon strategies
5. **Type Safety**: Full TypeScript support with proper types
