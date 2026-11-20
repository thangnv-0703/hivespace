@UC-001 @FR-AUTH-006
Feature: Verification link problems
  A link that cannot be used leads to a way to get a new one.

  Background:
    Given the storefront is shown in English

  Scenario: An invalid verification link offers a new one
    When a visitor opens the verification link with token "not-a-real-token"
    Then the page says "This link is not valid"
    And a "Send a new link" button is offered
