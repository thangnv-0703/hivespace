@UC-004 @FR-SHOP-005 @FR-SHOP-006 @FR-SHOP-007 @NFR-SEC-008
Feature: Shop settings: profile, pickup address and payout
  FEAT-010 through the real stack: a new seller completes the checklist from the dashboard, and the bank number is only ever shown masked.

  Background:
    Given the storefront is shown in English

  Scenario: A new seller saves the profile, the pickup address and the payout and the checklist completes
    When a visitor registers as "E2E User" with a new email address
    And the visitor opens the verification link
    And the visitor signs in from the home page
    And the user follows the "Seller centre" link in the header
    And the user registers a shop with a unique name as "Individual"
    Then the dashboard shows the new shop and the checklist "0 of 2 done"
    When the user opens "Add address" from the dashboard checklist
    Then the pickup and payout page is shown with both sections missing
    When the user saves the pickup address
    Then the pickup address badge says "Saved"
    When the user saves the payout with account number "0071 0012 34567"
    Then the payout shows the masked number "•••• 4567" and not the digits
    When the user opens "Shop profile" from the settings sidebar group
    And the user saves the profile description "Fresh bread every morning"
    Then the shop profile shows the description "Fresh bread every morning"
    When the user opens the dashboard
    Then the dashboard shows the checklist "2 of 2 done"
    And the dashboard says the shop is ready
