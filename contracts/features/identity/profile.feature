@service:identity
Feature: View and edit my profile and language
  A signed-in user reads and edits their profile and preferred language (UC-027).

  @operation:getMyProfile @FR-PROF-001 @FR-PROF-004
  Scenario: The profile of the signed-in user is returned
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user reads the profile
    Then the response status is 200
    And the profile has email "an@example.com", display name "An Nguyen" and language "vi"
    And the profile has no phone, gender, date of birth or avatar

  @operation:updateMyProfile @FR-PROF-002
  Scenario: Editing the profile saves every field and stores the phone as E.164
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with name "Lan Tran", phone "0912 345 678", gender "female" and date of birth "1995-04-30"
    Then the response status is 200
    When the user reads the profile
    Then the profile has display name "Lan Tran", phone "+84912345678", gender "female" and date of birth "1995-04-30"

  @operation:updateMyProfile @FR-PROF-002
  Scenario: Sending null clears phone, gender and date of birth
    Given a signed-in user with a complete profile
    When the user saves the profile with name "An Nguyen" and no phone, gender or date of birth
    Then the response status is 200
    And the profile has no phone, gender, date of birth or avatar

  @operation:updateMyProfile @FR-PROF-003
  Scenario: Every invalid field is reported in one response and nothing is saved
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with name "", phone "12345", gender "x" and date of birth tomorrow
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "display_name_required"
    And the details include "phone_invalid"
    And the details include "gender_invalid"
    And the details include "date_of_birth_in_future"
    When the user reads the profile
    Then the profile has email "an@example.com", display name "An Nguyen" and language "vi"

  @operation:updateMyProfile @FR-PROF-003
  Scenario Outline: Name length is limited to 80 characters
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with a name of <length> characters
    Then the response status is <status>

    Examples:
      | length | status |
      | 80     | 200    |
      | 81     | 400    |

  @operation:updateMyProfile @FR-PROF-003
  Scenario: A date of birth more than 120 years ago is rejected
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with a date of birth 121 years ago
    Then the response status is 400
    And the details include "date_of_birth_too_old"

  @operation:updateMyProfile @FR-PROF-003
  Scenario: An unparsable date of birth is rejected
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with date of birth "1995-02-31"
    Then the response status is 400
    And the details include "date_of_birth_invalid"

  @operation:updateMyProfile @FR-PROF-004
  Scenario: The email cannot be changed
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile with an extra email key "other@example.com"
    Then the response status is 400
    And the details include "email_read_only"
    When the user reads the profile
    Then the profile has email "an@example.com", display name "An Nguyen" and language "vi"

  @operation:updateMyProfile @FR-LOC-004
  Scenario: Errors come back in the request language
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the profile in language "en" with name "", phone "12345", gender "x" and date of birth tomorrow
    Then the response status is 400
    And the error message is "Some fields are not valid. Check the highlighted fields."

  @operation:updateMyLanguage @FR-PROF-006 @FR-LOC-002
  Scenario: Saving a language persists it and repeating it is harmless
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the language "en"
    Then the response status is 200
    And the language in the response is "en"
    When the user reads the profile
    Then the profile has email "an@example.com", display name "An Nguyen" and language "en"
    When the user saves the language "en"
    Then the response status is 200
    And exactly 1 UserProfileChanged event was queued

  @operation:updateMyLanguage @FR-PROF-006
  Scenario: An unsupported language is rejected
    Given a signed-in user with email "an@example.com" and name "An Nguyen"
    When the user saves the language "fr"
    Then the response status is 400
    And the details include "language_unsupported"

  @operation:getMyProfile @FR-AUTH-015
  Scenario Outline: Calls without a valid token are rejected
    When a caller reads the profile with <credential>
    Then the response status is 401
    And the error code is "common.unauthenticated"

    Examples:
      | credential         |
      | no token           |
      | a tampered token   |
      | a wrong-audience token |
