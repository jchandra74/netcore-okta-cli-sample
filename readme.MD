# OKTA (OAUTH2) enabled .NET Core CLI application Sample

The purpose of this sample is to demonstrate a CLI that is backed by Okta (or any OAuth2 provider).
We need strong authentication mechanism so we can log the action against a particular Okta user.

It will authenticate to Okta when:
1. It couldn't find ~/.cliplay/profile.json
2. It found ~/.cliplay/profile.json and the access token cached has expired.

Example of things that might be found in the profile.json

```JSON
{
    "access_token" : "ACCESS_TOKEN_VALUE_HERE",
    "expires_on" : UNIX_EPOCH_EXPIRY_UTC_TIMESTAMP_HERE,
    "username" : "AUTHENTICATED_USER_NAME_IN_OAUTH2_PROVIDER_HERE"
}
```

An example command has been added called `cleanup` and it is expecting 2 parameters: `--subscription` or `-s` and `--environment` or `-e`.

You can try it like so:
```bash
dotnet run cleanup --environment DEV --subscription "subscription name"
```

or
```bash
cliplay cleanup -e DEV -s "subscription name"
```

If you already built and publish the .NET Core app.