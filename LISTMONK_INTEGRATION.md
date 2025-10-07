# Listmonk Integration

This document describes how to configure and use the Listmonk integration in GenoCRM.

## Overview

GenoCRM automatically synchronizes members with your Listmonk mailing list instance. When a member's status is set to `Active`, they are automatically added to the configured newsletter list in Listmonk. When their status changes to any other state (Inactive, Terminated, etc.), they are automatically unsubscribed.

## Features

- **Automatic Sync on Status Change**: Members are synced to Listmonk whenever their status changes
- **Create/Update Members**: Creates new subscribers or updates existing ones in Listmonk
- **Automatic Subscription Management**: Subscribes active members and unsubscribes non-active members
- **Background Sync**: Hourly background job syncs all members to keep Listmonk in sync
- **Member Attributes**: Syncs member number, member type, and join date as custom attributes

## Configuration

### 1. appsettings.json

Add the following configuration to your `appsettings.json` or `appsettings.Development.json`:

```json
"Listmonk": {
  "Enabled": true,
  "BaseUrl": "https://your-listmonk-instance.com",
  "Username": "your-admin-username",
  "Password": "your-admin-password",
  "NewsletterListName": "BEW Newsletter - Mitglieder"
}
```

### 2. Environment Variables

Alternatively, you can configure via environment variables or `.env` file:

```bash
LISTMONK_ENABLED=true
LISTMONK_BASE_URL=https://your-listmonk-instance.com
LISTMONK_USERNAME=your-admin-username
LISTMONK_PASSWORD=your-admin-password
LISTMONK_NEWSLETTER_LIST_NAME="BEW Newsletter - Mitglieder"
```

### 3. Listmonk Setup

Before enabling the integration, ensure you have:

1. A running Listmonk instance
2. Created the newsletter list in Listmonk (e.g., "BEW Newsletter - Mitglieder")
3. Created an admin user with API access

## How It Works

### Automatic Synchronization

The integration automatically syncs members in the following scenarios:

1. **Member Created**: When a new member is created with `Active` status, they are added to Listmonk
2. **Status Changed**: When a member's status changes, they are subscribed (if Active) or unsubscribed (if not Active)
3. **Member Deleted**: When a member is soft-deleted, they are unsubscribed from the list
4. **Member Offboarded**: When a member is offboarded, they are unsubscribed from the list

### Background Sync

A background service runs every hour to sync all members with Listmonk. This ensures:

- Members manually added/changed in the database are synced
- Any sync failures are retried
- Listmonk stays in sync even if webhook/API calls fail

The background service runs 1 minute after application startup, then every hour.

### Member Status Mapping

| Member Status | Listmonk Action |
|--------------|-----------------|
| Active       | Subscribe to newsletter list |
| Applied      | Not subscribed |
| Inactive     | Unsubscribed from newsletter list |
| Suspended    | Unsubscribed from newsletter list |
| Offboarding  | Unsubscribed from newsletter list |
| Terminated   | Unsubscribed from newsletter list |
| Locked       | Unsubscribed from newsletter list |

### Custom Attributes

The following member data is synced to Listmonk as custom attributes:

- `member_number`: The member's unique number (e.g., "M001")
- `member_type`: Either "Individual" or "Company"
- `join_date`: The date the member joined (YYYY-MM-DD format)

## API Endpoints Used

The integration uses the following Listmonk API endpoints:

- `GET /api/lists` - Fetch all lists
- `GET /api/subscribers?query=...` - Search for subscribers by email
- `POST /api/subscribers` - Create a new subscriber
- `PUT /api/subscribers/{id}` - Update an existing subscriber
- `DELETE /api/subscribers/{id}` - Delete a subscriber

## Error Handling

- All Listmonk sync operations are wrapped in try-catch blocks
- Sync failures do not prevent member operations from completing
- Errors are logged for monitoring and debugging
- The background sync job will retry failed syncs on the next run

## Monitoring

Check your application logs for Listmonk sync activity:

```
Information: Synced active member 123 to Listmonk
Information: Unsubscribed member 456 from Listmonk newsletter
Information: Listmonk sync completed: 150 members synced, 0 errors
```

## Disabling the Integration

To disable the integration, set `Enabled` to `false` in configuration:

```json
"Listmonk": {
  "Enabled": false,
  ...
}
```

Or via environment variable:

```bash
LISTMONK_ENABLED=false
```

When disabled:
- No sync operations will be performed
- The background service will still run but skip all operations
- Member operations will continue normally without Listmonk integration

## Troubleshooting

### Members Not Syncing

1. Check that `Listmonk.Enabled` is set to `true`
2. Verify your Listmonk credentials are correct
3. Check application logs for error messages
4. Ensure the newsletter list exists in Listmonk with the exact name configured

### Wrong List Selected

1. Verify `NewsletterListName` matches exactly (case-sensitive)
2. Check that the list exists in your Listmonk instance
3. List names must match exactly including spaces and special characters

### Authentication Errors

1. Verify the username and password are correct
2. Ensure the user has admin privileges in Listmonk
3. Check that the Listmonk API is accessible from your GenoCRM server

## Security Considerations

- Store credentials securely using environment variables or secret management
- Use HTTPS for the Listmonk BaseUrl in production
- Consider using a dedicated API user with minimal required permissions
- Regularly rotate API credentials
