@UC-001 @UC-002 @FR-AUTH-001 @FR-AUTH-005 @FR-AUTH-009 @FR-AUTH-013
Feature: Register, verify the email, sign in and sign out
  The FEAT-001 hero flow through the real stack: storefront, gateway, Identity, broker, Notification and Mailpit.

  Background:
    Given the storefront is shown in English

  Scenario: A visitor registers, verifies by email link, signs in, keeps the session and signs out
    When a visitor registers as "E2E User" with a new email address
    Then the page says "Check your email"
    And the page shows the address the email was sent to
    And a verification email arrives with subject "Verify your HiveSpace email"
    When the visitor opens the verification link
    Then the page says "Email verified"
    When the visitor opens the verification link again
    Then the page says "Your email is already verified"
    When the visitor signs in from the home page
    Then the account menu shows the display name
    And the browser holds only HttpOnly session cookies
    And no token is readable by the page
    When the visitor reloads the page
    Then the account menu shows the display name
    When the visitor signs out
    Then the "Sign in" link is shown
    And opening the login route asks for credentials again
