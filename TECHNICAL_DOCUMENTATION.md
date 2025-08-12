# GenoCRM Technical Documentation

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture](#architecture)
3. [Domain Model](#domain-model)
4. [Service Layer](#service-layer)
5. [Authentication & Security](#authentication--security)
6. [Deployment & Infrastructure](#deployment--infrastructure)
7. [Testing Strategy](#testing-strategy)
8. [Developer Onboarding](#developer-onboarding)
9. [Configuration Reference](#configuration-reference)
10. [Troubleshooting](#troubleshooting)

---

## System Overview

GenoCRM is a comprehensive cooperative member management system built on .NET 9.0 Blazor with a hybrid Server/WebAssembly architecture. The system manages member relationships, share ownership, financial transactions, document management, and communications for cooperative organizations.

### Key Features
- **Member Management**: Complete member lifecycle management
- **Share Management**: Cooperative share certificates and transfers
- **Financial Operations**: Payments, dividends, and subordinated loans
- **Document Management**: Integrated with Nextcloud for document storage
- **Multi-Channel Messaging**: Email, SMS, and WhatsApp communications
- **Audit Trail**: Comprehensive audit logging for compliance
- **Multilingual Support**: English and German localization

### Technology Stack
- **Backend**: .NET 9.0, ASP.NET Core, Entity Framework Core
- **Frontend**: Blazor Server + WebAssembly (Hybrid)
- **Database**: SQLite with Entity Framework Core
- **Authentication**: OAuth 2.0 with Nextcloud integration
- **Document Storage**: Nextcloud WebDAV
- **Messaging**: SMTP, WhatsApp API, SMS providers
- **Logging**: Serilog with structured logging

---

## Architecture

### Project Structure
```
GenoCRM/
├── GenoCRM/                    # Main server application
│   ├── Components/             # Blazor components
│   │   ├── Layout/            # Layout components
│   │   ├── Pages/             # Page components
│   │   └── Shared/            # Shared components
│   ├── Controllers/           # API controllers
│   ├── Data/                  # Database context and configurations
│   ├── Models/                # Domain models and DTOs
│   ├── Services/              # Business services
│   └── wwwroot/               # Static assets
├── GenoCRM.Client/            # WebAssembly client
└── GenoCRM.Tests/             # Test project
```

### Hybrid Blazor Architecture
- **Server Components**: Interactive server-side components for complex operations
- **WebAssembly Components**: Client-side components for responsive UI
- **Shared Routing**: Centralized routing with assembly support
- **Static Assets**: Served from both server and client projects

### Key Design Patterns
- **Repository Pattern**: Data access through Entity Framework
- **Service Layer**: Business logic encapsulation
- **Dependency Injection**: Comprehensive DI throughout
- **Domain-Driven Design**: Rich domain models with business logic

---

## Domain Model

### Core Entities

#### Member
Central entity representing cooperative members (individuals or companies).

**Key Properties**:
- `MemberNumber` (string, unique): Auto-generated identifier (M001, M002, etc.)
- `MemberType` (enum): Individual or Company
- `Status` (enum): Active, Offboarding, Terminated
- `JoinDate`, `LeaveDate`, `TerminationNoticeDate`
- Individual fields: `FirstName`, `LastName`, `BirthDate`
- Company fields: `CompanyName`, `ContactPerson`

**Relationships**:
- One-to-many with `CooperativeShare`, `Payment`, `Dividend`
- Business rules enforced at entity level

#### CooperativeShare
Represents ownership shares in the cooperative.

**Key Properties**:
- `CertificateNumber` (string, unique): Auto-generated certificate identifier
- `Quantity` (int): Number of shares
- `NominalValue`, `Value` (decimal): Share pricing
- `Status` (enum): Active, Cancelled, Transferred, Suspended

**Business Logic**:
- Computed properties for payment tracking
- Support for transfers and consolidations

#### ShareTransfer
Manages share transfers between members with approval workflow.

**Key Properties**:
- `FromMemberId`, `ToMemberId`: Transfer parties
- `Status` (enum): Pending, Approved, Rejected, Completed
- `ApprovalDate`, `ApprovedBy`: Audit trail

#### Payment
Tracks all financial transactions in the system.

**Key Properties**:
- `PaymentNumber` (string, unique): Auto-generated identifier
- `Type` (enum): ShareCapital, SubordinatedLoan, Refund, Fee
- `Method` (enum): BankTransfer, Cash, Check
- `Status` (enum): Pending, Completed, Failed, Cancelled

#### Document
Document management with Nextcloud integration.

**Key Properties**:
- `Type` (enum): 15+ document types
- `Status` (enum): Active, Archived, Deleted, Expired
- `NextcloudPath`, `NextcloudShareLink`: Integration fields
- `IsConfidential`, `ExpirationDate`

#### Message
Multi-channel communication system.

**Key Properties**:
- `Type` (enum): 13 message types
- `Channel` (enum): Email, WhatsApp, SMS, Push
- `Status` (enum): Pending through Delivered/Read/Failed
- Delivery tracking and retry logic

### Database Schema
- **25+ tables** with comprehensive relationships
- **Indexes** optimized for performance
- **Global query filters** for soft deletion
- **Audit logging** for all entities
- **Automatic timestamps** for created/updated dates

---

## Service Layer

### Business Services

#### MemberService (`IMemberService`)
Core business logic for member management.

**Key Methods**:
- `GetMembersAsync()`: Retrieve members with filtering
- `CreateMemberAsync()`: Create new member with validation
- `UpdateMemberAsync()`: Update member information
- `SearchMembersAsync()`: Advanced search capabilities
- `SubmitTerminationNoticeAsync()`: Offboarding workflow

**Business Rules**:
- Automated member number generation
- Initial share creation on member creation
- 2-year termination notice period
- Fiscal year integration for termination timing

#### ShareTransferService (`IShareTransferService`)
Share transfer workflow management.

**Key Methods**:
- `CreateShareTransferRequestAsync()`: Initiate transfers
- `ApproveShareTransferAsync()`: Board approval
- `ExecuteShareTransferAsync()`: Complete transfers
- `GetPendingTransfersAsync()`: Approval queue

**Business Rules**:
- Multi-step approval process
- Member eligibility validation
- Automatic certificate generation

#### DocumentService (`IDocumentService`)
Document management with Nextcloud integration.

**Key Methods**:
- `UploadDocumentAsync()`: File upload with validation
- `DownloadDocumentAsync()`: Retrieve via Nextcloud
- `CreateDocumentVersionAsync()`: Version control
- `GetPublicShareLinkAsync()`: Public sharing

**Features**:
- File type and size validation
- Automatic organization by member/share
- SHA256 integrity checking
- Public sharing with expiration

#### MessagingService (`IMessagingService`)
Multi-channel messaging system.

**Key Methods**:
- `SendMessageAsync()`: Individual messages
- `SendMessageFromTemplateAsync()`: Template-based
- `CreateCampaignAsync()`: Bulk messaging
- `StartCampaignAsync()`: Campaign execution

**Features**:
- Multiple communication channels
- Template system with variables
- Delivery tracking and retry logic
- Campaign management

### Integration Services

#### NextcloudDocumentService (`INextcloudDocumentService`)
WebDAV-based document storage.

**Key Methods**:
- `UploadFileAsync()`: File upload to Nextcloud
- `DownloadFileAsync()`: File retrieval
- `CreateDirectoryAsync()`: Folder management
- `GetPublicShareLinkAsync()`: Sharing links

**Configuration**:
- WebDAV endpoint configuration
- Authentication credentials
- Path management for organized storage

#### NextcloudAuthService (`INextcloudAuthService`)
OAuth 2.0 authentication integration.

**Key Methods**:
- `GetUserInfoAsync()`: Profile retrieval
- `GetUserGroupsAsync()`: Group membership
- `SyncUserAsync()`: User synchronization
- `CreateClaimsPrincipal()`: Claims creation

### Messaging Providers
- **SmtpEmailProvider**: SMTP-based email delivery
- **WhatsAppProvider**: WhatsApp API integration
- **SmsProvider**: SMS gateway integration

---

## Authentication & Security

### Authentication Architecture

#### OAuth 2.0 Integration
- **Provider**: Nextcloud OAuth 2.0
- **Flow**: Authorization Code with PKCE
- **Scopes**: `openid`, `profile`, `email`
- **Session**: Cookie-based with 24-hour expiration

#### Authentication Flow
1. User accesses `/login` endpoint
2. Redirect to Nextcloud OAuth authorization
3. User authenticates and grants permissions
4. Authorization code exchange for access token
5. User profile and groups fetched from Nextcloud
6. Local user record created/updated
7. Permissions assigned based on group membership
8. Claims principal created with cookie authentication

### Authorization Model

#### Multi-Layer Authorization
1. **Group-Based Access Control**: Nextcloud groups
2. **Permission-Based Authorization**: 32 granular permissions
3. **Policy-Based Authorization**: ASP.NET Core policies
4. **Component-Level Authorization**: Razor component attributes

#### Permission System
**Key Permissions**:
- **Member Management**: `members.view`, `members.create`, `members.edit`, `members.delete`
- **Share Management**: `shares.view`, `shares.create`, `shares.transfer`, `shares.transfer.approve`
- **Financial Operations**: `dividends.view`, `dividends.calculate`, `dividends.pay`
- **Document Management**: `documents.view`, `documents.upload`, `documents.manage`
- **Administration**: `admin.users.manage`, `admin.permissions.manage`, `admin.audit.view`

#### Group Permission Mapping
**Configuration**: `Config/group-permissions.json`

**Key Groups**:
- **Entwickler (Developer)**: Full system access (36 permissions)
- **Vorstand (Board)**: Executive permissions
- **Aufsichtsrat (Supervisory Board)**: Oversight permissions
- **member**: Basic member permissions
- **accountant**: Financial permissions

### Security Features

#### Data Protection
- **Parameterized Queries**: SQL injection prevention
- **Global Query Filters**: Soft deletion and status filtering
- **Input Validation**: Model validation throughout
- **Audit Logging**: Comprehensive activity tracking

#### Communication Security
- **HTTPS Enforcement**: Automatic redirection
- **HSTS**: HTTP Strict Transport Security
- **CSRF Protection**: Antiforgery tokens
- **Secure Cookies**: HttpOnly, SameSite, Secure flags

#### Audit and Compliance
- **Comprehensive Audit Trail**: All business operations logged
- **User Context**: IP address, user agent tracking
- **Change Tracking**: JSON serialization of changes
- **Structured Logging**: Serilog with file and console output

---

## Deployment & Infrastructure

### Docker Configuration

#### Multi-Stage Dockerfile
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["GenoCRM/GenoCRM/GenoCRM.csproj", "GenoCRM/GenoCRM/"]
COPY ["GenoCRM/GenoCRM.Client/GenoCRM.Client.csproj", "GenoCRM/GenoCRM.Client/"]
RUN dotnet restore "GenoCRM/GenoCRM/GenoCRM.csproj"
COPY . .
WORKDIR "/src/GenoCRM/GenoCRM"
RUN dotnet build "./GenoCRM.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./GenoCRM.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GenoCRM.dll"]
```

#### Build Commands
```bash
# Build Docker image
docker build -t genocrm .

# Run in container
docker run -p 8080:8080 genocrm

# Development run
docker run -p 8080:8080 -v $(pwd)/logs:/app/logs genocrm
```

### Development Environment

#### Prerequisites
- .NET 9.0 SDK
- Docker (optional)
- Nextcloud instance for authentication and document storage

#### Development URLs
- HTTP: `http://localhost:5015`
- HTTPS: `https://localhost:7273`

#### Common Commands
```bash
# Build solution
dotnet build

# Run application
dotnet run --project GenoCRM/GenoCRM/GenoCRM.csproj

# Run with HTTPS profile
dotnet run --project GenoCRM/GenoCRM/GenoCRM.csproj --launch-profile https

# Run tests
dotnet test

# Publish for deployment
dotnet publish GenoCRM/GenoCRM/GenoCRM.csproj -c Release
```

### Configuration Management

#### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `ConnectionStrings__DefaultConnection`: Database connection
- `NextcloudAuth__ClientId`: OAuth client ID
- `NextcloudAuth__ClientSecret`: OAuth client secret
- `Nextcloud__Username`: Service account username
- `Nextcloud__Password`: Service account password

#### Configuration Files
- `appsettings.json`: Base configuration
- `appsettings.Development.json`: Development overrides
- `appsettings.Production.json`: Production overrides
- `Config/group-permissions.json`: Permission mappings

### Database Management

#### Entity Framework Migrations
```bash
# Add migration
dotnet ef migrations add <MigrationName> --project GenoCRM/GenoCRM

# Update database
dotnet ef database update --project GenoCRM/GenoCRM

# Generate SQL script
dotnet ef migrations script --project GenoCRM/GenoCRM
```

#### Database Seeding
- Development: Automatic seeding on startup
- Production: Manual seeding via migrations

---

## Testing Strategy

### Test Architecture

#### Framework Stack
- **xUnit**: Primary testing framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking framework
- **bUnit**: Blazor component testing
- **EF Core InMemory**: Database testing
- **ASP.NET Core Testing**: Integration testing

#### Test Categories
1. **Unit Tests**: Business logic validation
2. **Integration Tests**: Service layer testing
3. **Component Tests**: Blazor component testing
4. **Authorization Tests**: Permission validation

### Test Organization

#### Business Service Tests
- **MemberServiceIntegrationTests**: Full member workflow testing
- **ShareTransferServiceTests**: 18 comprehensive test methods
- **ShareApprovalServiceTests**: 14 approval workflow tests
- **ShareServiceCertificateTests**: Certificate generation testing

#### Integration Tests
- **ShareTransferMemberLockingTests**: Complex workflow testing
- **Authorization Tests**: Permission and policy validation

#### Test Patterns
- **Arrange-Act-Assert**: Standard test structure
- **In-Memory Database**: Isolated test databases
- **Dependency Injection**: Full DI container setup
- **Builder Pattern**: Test data creation helpers

### Test Execution

#### Running Tests
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=ShareTransferServiceTests"

# Run tests with detailed output
dotnet test --verbosity normal
```

#### Test Coverage
- **Service Layer**: Comprehensive coverage
- **Business Logic**: Complex scenarios tested
- **Authorization**: Permission validation
- **Integration**: End-to-end workflows

### CI/CD Recommendations

#### GitHub Actions Pipeline
```yaml
name: CI/CD Pipeline
on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 9.0.x
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

---

## Developer Onboarding

### Getting Started

#### 1. Environment Setup
1. **Install .NET 9.0 SDK**
   ```bash
   # Download from https://dotnet.microsoft.com/download
   dotnet --version  # Verify installation
   ```

2. **Clone Repository**
   ```bash
   git clone <repository-url>
   cd GenoCRM
   ```

3. **Install Dependencies**
   ```bash
   dotnet restore
   ```

4. **Configure Development Environment**
   - Copy `appsettings.Development.json.example` to `appsettings.Development.json`
   - Configure Nextcloud connection settings
   - Set up database connection string

#### 2. First Run
```bash
# Build the solution
dotnet build

# Run the application
dotnet run --project GenoCRM/GenoCRM/GenoCRM.csproj

# Navigate to https://localhost:7273
```

#### 3. Development Workflow
1. **Database Migrations**: Create migrations for schema changes
2. **Testing**: Write tests for new functionality
3. **Code Review**: Follow established patterns and conventions
4. **Documentation**: Update technical documentation as needed

### Code Conventions

#### C# Conventions
- **Naming**: PascalCase for classes, methods, properties
- **Interfaces**: Prefix with `I` (e.g., `IMemberService`)
- **Async Methods**: Suffix with `Async`
- **Constants**: PascalCase with descriptive names

#### Blazor Conventions
- **Components**: PascalCase file names
- **Parameters**: Use `[Parameter]` attribute
- **Event Callbacks**: Use `EventCallback<T>`
- **CSS**: Co-located CSS files with `.razor.css`

#### Service Layer Conventions
- **Interface-First**: Always define interfaces for services
- **Dependency Injection**: Use constructor injection
- **Error Handling**: Comprehensive exception handling
- **Logging**: Structured logging with context

### Architecture Patterns

#### Domain-Driven Design
- **Rich Domain Models**: Business logic in entities
- **Service Layer**: Business operations encapsulation
- **Repository Pattern**: Data access abstraction
- **Value Objects**: Immutable data structures

#### Security Patterns
- **Permission-Based Authorization**: Granular access control
- **Audit Logging**: Comprehensive activity tracking
- **Input Validation**: Validate all user inputs
- **Secure Defaults**: Fail-safe security posture

### Development Tools

#### Recommended IDE Extensions
- **C# Extensions**: OmniSharp, C# Dev Kit
- **Blazor Tools**: Blazor debugging support
- **Entity Framework**: EF Core Power Tools
- **Testing**: Test Explorer, Coverage tools

#### Debugging
- **Blazor Server**: Full debugging support
- **Blazor WebAssembly**: Browser debugging
- **Database**: SQL Server Object Explorer
- **Logging**: Real-time log viewing

---

## Configuration Reference

### Application Settings

#### Database Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=genocrm.db"
  }
}
```

#### Nextcloud Integration
```json
{
  "Nextcloud": {
    "BaseUrl": "https://your-nextcloud.com",
    "WebDAVUrl": "https://your-nextcloud.com/remote.php/dav/files/username/",
    "Username": "service-account",
    "Password": "service-password",
    "DocumentsPath": "/Documents/GenoCRM",
    "MaxFileSize": 10485760,
    "AllowedExtensions": [".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".txt"]
  }
}
```

#### OAuth Authentication
```json
{
  "NextcloudAuth": {
    "BaseUrl": "https://your-nextcloud.com",
    "AuthorizeEndpoint": "https://your-nextcloud.com/apps/oauth2/authorize",
    "TokenEndpoint": "https://your-nextcloud.com/apps/oauth2/api/v1/token",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```

#### Messaging Configuration
```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "EnableSsl": true,
    "FromEmail": "noreply@your-cooperative.com",
    "FromName": "Your Cooperative"
  },
  "WhatsApp": {
    "AccessToken": "YOUR_WHATSAPP_ACCESS_TOKEN",
    "ApiUrl": "https://graph.facebook.com/v18.0/YOUR_PHONE_NUMBER_ID/messages",
    "PhoneNumberId": "YOUR_PHONE_NUMBER_ID"
  },
  "Sms": {
    "ApiKey": "YOUR_SMS_PROVIDER_API_KEY",
    "ApiUrl": "https://api.your-sms-provider.com/v1/messages",
    "FromNumber": "+49123456789"
  }
}
```

#### Business Rules Configuration
```json
{
  "CooperativeSettings": {
    "ShareDenomination": 250.00,
    "MaxSharesPerMember": 100
  },
  "FiscalYear": {
    "StartMonth": 1,
    "StartDay": 1
  }
}
```

### Environment Variables

#### Production Environment
```bash
# Database
ConnectionStrings__DefaultConnection="Data Source=/app/data/genocrm.db"

# Authentication
NextcloudAuth__ClientId="production-client-id"
NextcloudAuth__ClientSecret="production-client-secret"

# Nextcloud
Nextcloud__Username="service-account"
Nextcloud__Password="secure-password"

# Logging
Serilog__MinimumLevel="Warning"
```

#### Development Environment
```bash
# Enable detailed errors
ASPNETCORE_ENVIRONMENT="Development"

# Database
ConnectionStrings__DefaultConnection="Data Source=genocrm-dev.db"

# Logging
Serilog__MinimumLevel="Debug"
```

---

## Troubleshooting

### Common Issues

#### Authentication Problems
**Issue**: OAuth authentication fails
**Solution**: 
1. Verify Nextcloud OAuth app configuration
2. Check client ID and secret
3. Ensure redirect URLs are correct
4. Verify Nextcloud user has required groups

#### Database Issues
**Issue**: Database connection fails
**Solution**:
1. Check connection string format
2. Verify database file permissions
3. Ensure migrations are applied
4. Check Entity Framework logs

#### Document Upload Failures
**Issue**: File uploads to Nextcloud fail
**Solution**:
1. Verify WebDAV endpoint URL
2. Check service account credentials
3. Ensure directory permissions
4. Validate file size and type restrictions

#### Permission Errors
**Issue**: Authorization failures
**Solution**:
1. Check user group membership in Nextcloud
2. Verify group-permissions.json configuration
3. Ensure permission policies are registered
4. Check audit logs for permission context

### Logging and Monitoring

#### Log Locations
- **Console**: Real-time application logs
- **File**: `logs/genocrm-{Date}.log`
- **Database**: Audit logs in `AuditLogs` table

#### Log Analysis
```bash
# View recent logs
tail -f logs/genocrm-$(date +%Y%m%d).log

# Search for errors
grep "ERROR" logs/genocrm-*.log

# Filter authentication issues
grep "Authentication" logs/genocrm-*.log
```

#### Performance Monitoring
- **Database Performance**: Entity Framework logging
- **Request Timing**: ASP.NET Core request logging
- **Memory Usage**: Application performance counters
- **External Service Calls**: HTTP client logging

### Development Debugging

#### Blazor Debugging
- **Server**: Full Visual Studio debugging
- **WebAssembly**: Browser developer tools
- **Hybrid**: Component-specific debugging

#### Database Debugging
- **SQL Logging**: Enable EF Core command logging
- **Query Analysis**: Use SQL profiling tools
- **Migration Issues**: Check migration history

#### Service Testing
- **Integration Tests**: Run service layer tests
- **API Testing**: Use Postman or similar tools
- **Unit Tests**: Test individual components

---

## Performance Considerations

### Database Optimization
- **Indexes**: Optimized for common queries
- **Query Filters**: Global filters for performance
- **Pagination**: Implemented for large datasets
- **Connection Pooling**: Efficient connection management

### Caching Strategy
- **Static Assets**: Browser caching enabled
- **API Responses**: Consider adding response caching
- **Database Queries**: Optimize with proper indexing
- **Session State**: Efficient session management

### Scalability
- **Stateless Design**: Scalable service architecture
- **External Dependencies**: Resilient integration patterns
- **Resource Management**: Proper disposal patterns
- **Load Balancing**: Container-ready for scaling

---

This technical documentation provides comprehensive guidance for developers and DevOps personnel working with the GenoCRM system. For additional support or questions, please refer to the project repository or contact the development team.