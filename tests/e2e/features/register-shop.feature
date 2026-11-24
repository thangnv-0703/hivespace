@UC-003 @FR-SHOP-001 @FR-SHOP-004 @FR-AUTH-009
Feature: Register a shop and become a seller
  FEAT-009 through the real stack: storefront, the seller host, gateway session refresh, Identity (shop, Seller role) and the broker.

  Background:
    Given the storefront is shown in English

  Scenario: A signed-in buyer opens a shop from the storefront header and lands on the dashboard checklist
    When a visitor registers as "E2E User" with a new email address
    Then the page says "Check your email"
    And a verification email arrives with subject "Verify your HiveSpace email"
    When the visitor opens the verification link
    Then the page says "Email verified"
    When the visitor signs in from the home page
    Then the account menu shows the display name
    When the user follows the "Seller centre" link in the header
    Then the seller centre asks for a shop name and no password
    When the user registers a shop with a unique name as "Individual"
    Then the dashboard shows the new shop and the checklist "0 of 2 done"
    When the user opens the registration page again
    Then the dashboard is shown instead
