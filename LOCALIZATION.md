# GenoCRM Localization Implementation

## Overview
GenoCRM now supports English and German localization using ASP.NET Core's built-in localization framework.

## Features Implemented

### 1. Localization Infrastructure
- **Program.cs**: Configured localization services and middleware
- **Supported Cultures**: English (en) and German (de)
- **Culture Providers**: Query string, cookie, and Accept-Language header

### 2. Resource Files
- **SharedResource.resx/de.resx**: Common UI elements and navigation
- **Members.resx/de.resx**: Member-specific translations

### 3. Services
- **CultureService**: Manages culture switching and detection
- **FormattingService**: Provides culture-specific formatting for dates, numbers, and currency

### 4. Components
- **CultureSwitcher**: Dropdown component for language switching
- **NavMenu**: Localized navigation with culture switcher
- **MemberList**: Fully localized member listing page

### 5. Localized Elements
- Navigation menu items
- Page titles and headings
- Form labels and buttons
- Status indicators
- Date and currency formatting
- Search placeholders and messages

## Usage

### Switching Languages
Users can switch languages using the dropdown in the top navigation bar. The selection is saved in a cookie and persists across sessions.

### Adding New Translations
1. Add entries to appropriate .resx files
2. Use `IStringLocalizer<ResourceType>` in components
3. Follow the pattern: `@Localizer["KeyName"]`

### Culture-Specific Formatting
- Dates: Automatically formatted per culture
- Currency: Euro (€) for German, system default for others
- Numbers: Locale-appropriate decimal separators

## File Structure
```
Resources/
├── SharedResource.resx (English - fallback)
├── SharedResource.de.resx (German)
├── SharedResource.cs (Resource class)
├── Pages/
│   ├── Members.resx (English)
│   ├── Members.de.resx (German)
│   └── Members.cs (Resource class)

Services/Localization/
├── CultureService.cs
└── FormattingService.cs

Components/Shared/
└── CultureSwitcher.razor
```

## Technical Notes
- Uses cookie-based culture persistence
- Falls back to English if requested culture is not supported
- JavaScript page reload required for culture changes to take effect
- All services are registered in DI container
- Resource files follow standard .NET localization patterns