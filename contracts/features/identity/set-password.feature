@service:identity
Feature: Set a first password
  A user whose account has no password sets one after a Google sign-in in the last 10 minutes (FR-AUTH-027, UC-026 1a).

  @operation:setMyPassword @FR-AUTH-027
  Scenario: A recent Google sign-in sets the first password
    Given a Google-only user "an@example.com" who signed in with Google just now
    When the user sets the password "world5678"
    Then the response status is 204
    And exactly 1 PasswordChanged event was queued
    When the visitor signs in with "an@example.com" and "world5678"
    Then the response status is 200

  @operation:setMyPassword @FR-AUTH-027
  Scenario: A password sign-in is not enough
    Given a signed-in user with email "an@example.com" and password "hello1234"
    And the account has no password
    When the user sets the password "world5678"
    Then the response status is 403
    And the error code is "identity.reauthentication_required"
    And exactly 0 PasswordChanged event was queued

  @operation:setMyPassword @FR-AUTH-027
  Scenario: A Google sign-in older than 10 minutes is not enough
    Given a Google-only user "an@example.com" whose Google sign-in was 11 minutes ago
    When the user sets the password "world5678"
    Then the response status is 403
    And the error code is "identity.reauthentication_required"

  @operation:setMyPassword @FR-AUTH-027
  Scenario: An account that already has a password cannot set one
    Given a Google-only user "an@example.com" who signed in with Google just now
    And the account has a password "hello1234"
    When the user sets the password "world5678"
    Then the response status is 409
    And the error code is "identity.password_already_set"

  @operation:setMyPassword @FR-AUTH-003
  Scenario: Every unmet password rule is reported together
    Given a Google-only user "an@example.com" who signed in with Google just now
    When the user sets the password "abc"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "password_too_short"
    And the details include "password_needs_digit"
    And exactly 0 PasswordChanged event was queued

  @operation:setMyPassword @FR-AUTH-015
  Scenario: A call without a token is rejected
    When a caller sets the password "world5678" with no token
    Then the response status is 401
    And the error code is "common.unauthenticated"

  @operation:getMyProfile @FR-AUTH-027
  Scenario: The profile says whether the account has a password
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user reads the profile
    Then the profile has a password
    Given the account has no password
    When the user reads the profile
    Then the profile has no password

  @operation:setMyPassword @FR-LOC-004
  Scenario: The re-authentication message comes back in the request language
    Given a signed-in user with email "an@example.com" and password "hello1234"
    And the account has no password
    When the user sets the password "world5678" in language "vi"
    Then the response status is 403
    And the error message is "Để bảo mật, hãy xác nhận đó là bạn bằng Google trước."
