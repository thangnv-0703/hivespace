@UC-002 @FR-AUTH-010
Feature: Sign-in failures
  Every failed sign-in gives the same generic answer.

  Background:
    Given the storefront is shown in English

  Scenario: A wrong password shows one generic error and keeps the email
    When a visitor opens the login route
    And the visitor submits email "nobody@example.com" and password "not-a-password-1"
    Then an alert says "Sign-in failed"
    And the email field still holds "nobody@example.com"
