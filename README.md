# Advanced Users Management

This demo shows how to create custom logics related to users in FactoryTalk Optix. It demonstrates how to create a custom user type with additional properties that allows to:

- Manually disable the user
- Force a change password at first login
- Automatically block the user after a number of failed login attempts
- Automatically unblock the user after a certain amount of time
- Specify a custom regex for the password
- Implement the Levenshtein distance algorithm to check the similarity between the old and new password

In addition to the custom user type, the demo also shows how to create and dispatch custom `UserSessionEvents` to trace the user's activity in the system. To get more information about OPC/UA events dispatching, please consult the `Dispatch OPC/UA events` section of the `NetLogic Cheatsheet` repository.

## Disclaimer

Rockwell Automation maintains these repositories as a convenience to you and other users. Although Rockwell Automation reserves the right at any time and for any reason to refuse access to edit or remove content from this Repository, you acknowledge and agree to accept sole responsibility and liability for any Repository content posted, transmitted, downloaded, or used by you. Rockwell Automation has no obligation to monitor or update Repository content

The examples provided are to be used as a reference for building your own application and should not be used in production as-is. It is recommended to adapt the example for the purpose and observing the highest safety standards.
