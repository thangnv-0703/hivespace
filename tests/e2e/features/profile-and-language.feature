@UC-027 @FR-PROF-001 @FR-PROF-002 @FR-PROF-003 @FR-PROF-004 @FR-PROF-006 @FR-LOC-002
Feature: Edit the profile and keep the preferred language
  FEAT-002 through the real stack: storefront, gateway session refresh, Identity bearer validation, broker and Notification.

  Background:
    Given the storefront is shown in English

  Scenario: A user edits the profile, picks Vietnamese, and it follows them to another browser
    When a visitor registers as "E2E User" with a new email address
    Then the page says "Check your email"
    And a verification email arrives with subject "Verify your HiveSpace email"
    When the visitor opens the verification link
    Then the page says "Email verified"
    When the visitor signs in from the home page
    Then the account menu shows the display name
    When the user opens the profile page from the account menu
    Then the page says "My profile"
    And the email field is read-only and shows the registration address
    When the user saves the profile with name "Lan Tran", phone "0912 345 678", gender "Female" and date of birth 30 April 1995
    Then a toast says "Profile saved"
    And the account menu shows the updated name "Lan Tran"
    When the visitor reloads the page
    Then the phone field shows "+84 912 345 678"
    When the user tries to save the date of birth 31 February 1995
    Then the date of birth shows the error "This date does not exist."
    When the user picks "Tiếng Việt" as the language
    Then the page says "Hồ sơ của tôi"
    When the visitor reloads the page
    Then the page says "Hồ sơ của tôi"
    And signing in from a fresh browser context shows the profile in Vietnamese
