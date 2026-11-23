@UC-025 @FR-AUTH-020 @FR-AUTH-021 @FR-NOTIF-015
Feature: Sign in with Google
  UC-025 through the real stack against the fake IdP (tools/fake-idp) standing in for Google (architecture §8.6).

  Background:
    Given the storefront is shown in English

  Scenario: A new visitor continues with Google and ends signed in
    Given Google will sign in a new person named "Google User"
    When the visitor continues with Google from the home page
    Then the account menu shows "Google User"

  Scenario: A visitor with a password account of the same email is linked and signed in
    Given a visitor registered with a password as "E2E User"
    And Google will sign in the same email named "Someone Else"
    When the visitor continues with Google from the home page
    Then the account menu shows "E2E User"
    And an email arrives with subject "Google account linked to HiveSpace"
