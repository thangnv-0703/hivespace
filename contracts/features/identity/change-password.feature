@service:identity
Feature: Change my password
  A signed-in user changes their password by giving the current one (UC-026). Other sessions are signed out (FR-AUTH-024).

  @operation:changeMyPassword @FR-AUTH-024
  Scenario: A correct change signs in with the new password only
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "hello1234" to "world5678"
    Then the response status is 204
    When the visitor signs in with "an@example.com" and "world5678"
    Then the response status is 200
    When the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 401
    And the error code is "identity.invalid_credentials"
    And exactly 1 PasswordChanged event was queued

  @operation:changeMyPassword @FR-AUTH-025
  Scenario: A wrong current password is reported on its field and nothing changes
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "wrong9999" to "world5678"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "current_password_incorrect"
    And exactly 0 PasswordChanged event was queued
    When the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 200

  @operation:changeMyPassword @FR-AUTH-025
  Scenario: An empty current password is required
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "" to "world5678"
    Then the response status is 400
    And the details include "current_password_required"

  @operation:changeMyPassword @FR-AUTH-003
  Scenario: Every unmet password rule is reported together
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "hello1234" to "abc"
    Then the response status is 400
    And the details include "password_too_short"
    And the details include "password_needs_digit"

  @operation:changeMyPassword @FR-AUTH-003
  Scenario Outline: A password needs a letter and a digit
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "hello1234" to "<password>"
    Then the response status is 400
    And the details include "<code>"

    Examples:
      | password | code                 |
      | abcdefgh | password_needs_digit |
      | 12345678 | password_needs_letter |

  @operation:changeMyPassword @FR-AUTH-003
  Scenario: A password longer than 128 characters is rejected
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "hello1234" to a password of 129 characters
    Then the response status is 400
    And the details include "password_too_long"

  @operation:changeMyPassword @FR-AUTH-025
  Scenario: A wrong current password and a bad new password are reported together
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password from "wrong9999" to "abc"
    Then the response status is 400
    And the details include "current_password_incorrect"
    And the details include "password_too_short"
    And the details include "password_needs_digit"

  @operation:changeMyPassword @FR-AUTH-026
  Scenario: An account without a password cannot change it
    Given a signed-in user with email "an@example.com" and password "hello1234"
    And the account has no password
    When the user changes the password from "hello1234" to "world5678"
    Then the response status is 409
    And the error code is "identity.password_not_set"

  @operation:changeMyPassword @FR-AUTH-015
  Scenario Outline: Calls without a valid token are rejected
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When a caller changes the password with <credential>
    Then the response status is 401
    And the error code is "common.unauthenticated"

    Examples:
      | credential       |
      | no token         |
      | a tampered token |

  @operation:changeMyPassword @FR-LOC-004
  Scenario: Errors come back in the request language
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password in language "vi" from "wrong9999" to "world5678"
    Then the response status is 400
    And the error message is "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường được đánh dấu."

  @operation:changeMyPassword @FR-AUTH-025 @FR-AUTH-011
  Scenario: Wrong current passwords count toward the lockout
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the visitor signs in wrongly 3 times as "an@example.com"
    And the user changes the password from "wrong9999" to "world5678"
    Then the response status is 400
    When the user changes the password from "wrong9999" to "world5678"
    Then the response status is 423
    And the error code is "identity.account_locked"

  @operation:changeMyPassword @FR-AUTH-012
  Scenario: A locked account cannot change its password
    Given a signed-in user with email "an@example.com" and password "hello1234"
    And the account is locked
    When the user changes the password from "hello1234" to "world5678"
    Then the response status is 423
    And the error code is "identity.account_locked"
    And exactly 0 PasswordChanged event was queued

  @operation:changeMyPassword @FR-LOC-004
  Scenario: Errors come back in English when asked
    Given a signed-in user with email "an@example.com" and password "hello1234"
    When the user changes the password in language "en" from "wrong9999" to "world5678"
    Then the response status is 400
    And the error message is "Some fields are not valid. Check the highlighted fields."
