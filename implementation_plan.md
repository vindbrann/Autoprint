# Implementation Plan - Refactoring Users and DTOs

## Goal Description
Refactor `UsersController.cs` to remove internally defined DTOs (`CreateUserDto`, `UpdateUserDto`) and instead use the shared DTOs located in `Autoprint.Shared/DTOs/UserDtos.cs`. This ensures consistency between the server and client (Blazor) which shares this project.

## User Review Required
> [!NOTE]
> This is a refactoring task. No functional changes are expected, but it ensures better code maintainability.

## Proposed Changes

### Autoprint.Shared
#### [MODIFY] [UserDtos.cs](file:///d:/code/Antigravity/Autoprint/Autoprint.Shared/DTOs/UserDtos.cs)
- Verify that `CreateUserDto` and `UpdateUserDto` in the shared project match the requirements of the Controller.
- If fields are missing, add them.

### Autoprint.Server
#### [MODIFY] [UsersController.cs](file:///d:/code/Antigravity/Autoprint/Autoprint.Server/Controllers/UsersController.cs)
- Remove internal `CreateUserDto` and `UpdateUserDto` classes.
- Update action signatures to use `Autoprint.Shared.DTOs.CreateUserDto` and `Autoprint.Shared.DTOs.UpdateUserDto`.
- Ensure namespace `Autoprint.Shared.DTOs` is used.

## Verification Plan

### Automated Tests
- Build the solution to ensure no compilation errors.
- Since there are no automated tests mentioned in the onboarding, I will rely on the build.

### Manual Verification
- I will verify that the `UsersController` compiles correctly.
