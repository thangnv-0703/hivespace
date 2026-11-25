@service:identity
Feature: Shop settings: profile, pickup address and payout
  A seller completes the shop's profile, pickup address and payout bank details (UC-004).

  Background:
    Given a signed-in user with a verified email "an@example.com"
    And the user registered a shop named "Tiệm Bánh An" of type "individual" and refreshed the token

  @operation:getMyShop @FR-SHOP-010
  Scenario: A new shop has no settings yet
    When the user reads the shop
    Then the response status is 200
    And the shop has no description, no pickup address and no payout and an unfinished setup

  @operation:updateMyShopProfile @FR-SHOP-005
  Scenario: The seller updates the name and description
    When the user saves the shop profile with name "Tiệm Bánh An 2" and description "Bánh mì nóng\nmỗi sáng"
    Then the response status is 200
    And the shop profile is name "Tiệm Bánh An 2" and description "Bánh mì nóng\nmỗi sáng"
    When the user reads the shop
    Then the shop profile is name "Tiệm Bánh An 2" and description "Bánh mì nóng\nmỗi sáng"
    And exactly 1 ShopChanged event was queued

  @operation:updateMyShopProfile @FR-SHOP-005
  Scenario: A null description clears it and an identical repeat queues nothing new
    When the user saves the shop profile with name "Tiệm Bánh An" and description "Hello"
    And the user saves the shop profile with name "Tiệm Bánh An" and no description
    Then the response status is 200
    And the shop profile is name "Tiệm Bánh An" and no description
    When the user saves the shop profile with name "Tiệm Bánh An" and no description
    Then the response status is 200
    And exactly 2 ShopChanged events were queued

  @operation:updateMyShopProfile @FR-SHOP-005 @FR-SHOP-003
  Scenario: Renaming to another shop's name is a conflict and changes nothing
    Given another user owns a shop named "Tiệm Bún Bò"
    When the user saves the shop profile with name "  tiệm  bún bò " and no description
    Then the response status is 409
    And the error code is "identity.shop_name_taken"
    When the user reads the shop
    Then the shop profile is name "Tiệm Bánh An" and no description
    And exactly 0 ShopChanged events were queued

  @operation:updateMyShopProfile @FR-SHOP-003
  Scenario: Saving the shop's own name in another case is accepted
    When the user saves the shop profile with name "TIỆM BÁNH AN" and no description
    Then the response status is 200
    And the shop profile is name "TIỆM BÁNH AN" and no description

  @operation:updateMyShopProfile @FR-SHOP-008
  Scenario Outline: Description rules
    When the user saves the shop profile with a description of <length> characters
    Then the response status is <status>

    Examples:
      | length | status |
      | 1000   | 200    |
      | 1001   | 400    |

  @operation:updateMyShopProfile @FR-SHOP-008
  Scenario: Invalid profile fields are all reported and nothing is saved
    When the user saves the shop profile with name "ab" and a description with a control character
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "shop_name_too_short"
    And the details include "shop_description_invalid"
    And exactly 0 ShopChanged events were queued

  @operation:updateMyShopProfile @FR-SHOP-008
  Scenario: A missing key, an unknown key or a wrong type is a field error
    When the user saves the shop profile with only a name
    Then the response status is 400
    And the details name the field "description"
    When the user saves the shop profile with an extra key "shopId"
    Then the response status is 400
    And the details name the field "shopId"
    When the user saves the shop profile with a number as the name
    Then the response status is 400
    And the details name the field "name"

  @operation:setMyShopPickupAddress @FR-SHOP-006
  Scenario: The seller sets the pickup address
    When the user saves the pickup address with phone "0912 345 678" and street "  12  Lê Lợi "
    Then the response status is 200
    And the pickup address has phone "+84912345678" and street "12 Lê Lợi"
    And the setup shows the pickup address done and the payout not done
    When the user reads the shop
    Then the pickup address has phone "+84912345678" and street "12 Lê Lợi"
    And exactly 0 ShopChanged events were queued

  @operation:setMyShopPickupAddress @FR-SHOP-008
  Scenario: Every invalid address field is reported and nothing is saved
    When the user saves a pickup address with every field invalid
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "address_field_required"
    And the details include "address_field_too_long"
    And the details include "address_field_invalid"
    And the details include "phone_invalid"
    And the details name the field "contactName"
    And the details name the field "phone"
    And the details name the field "province"
    And the details name the field "district"
    And the details name the field "ward"
    And the details name the field "street"
    When the user reads the shop
    Then the shop has no description, no pickup address and no payout and an unfinished setup

  @operation:setMyShopPickupAddress @FR-SHOP-008
  Scenario: An unknown key is a field error
    When the user saves the pickup address with an extra key "shopId"
    Then the response status is 400
    And the details name the field "shopId"

  @operation:setMyShopPayout @FR-SHOP-007 @NFR-SEC-008
  Scenario: The seller sets the payout and only the last four digits come back
    When the user saves the payout with bank "Vietcombank", account number "0071 0012 34567" and holder "NGUYEN VAN AN"
    Then the response status is 200
    And the payout shows bank "Vietcombank", last four "4567" and holder "NGUYEN VAN AN"
    And the response does not contain the account number "0071001234567"
    And the setup shows the pickup address not done and the payout done
    And the stored account number is encrypted and does not contain "0071001234567"
    When the user reads the shop
    Then the payout shows bank "Vietcombank", last four "4567" and holder "NGUYEN VAN AN"
    And the response does not contain the account number "0071001234567"

  @operation:setMyShopPayout @FR-SHOP-008
  Scenario Outline: Account number rules
    When the user saves the payout with bank "Vietcombank", account number "<number>" and holder "NGUYEN VAN AN"
    Then the response status is <status>

    Examples:
      | number                | status |
      | 12345                 | 400    |
      | 123456                | 200    |
      | 1234567890123456789   | 200    |
      | 12345678901234567890  | 400    |
      | 12ab                  | 400    |
      | 123-456 789           | 200    |

  @operation:setMyShopPayout @FR-SHOP-008
  Scenario: Every invalid payout field is reported and nothing is saved
    When the user saves a payout with every field invalid
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "bank_name_too_long"
    And the details include "bank_account_number_invalid"
    And the details include "bank_account_holder_required"
    When the user reads the shop
    Then the shop has no description, no pickup address and no payout and an unfinished setup

  @operation:setMyShopPayout @FR-SHOP-008
  Scenario: A missing account number is required
    When the user saves the payout with bank "Vietcombank", account number "" and holder "NGUYEN VAN AN"
    Then the response status is 400
    And the details include "bank_account_number_required"

  @operation:setMyShopPayout @FR-SHOP-011
  Scenario: The shop becomes ready once the address and the payout are both saved
    When the user saves the pickup address with phone "0912345678" and street "12 Lê Lợi"
    And the user saves the payout with bank "Vietcombank", account number "0071001234567" and holder "NGUYEN VAN AN"
    Then the response status is 200
    And the setup shows the pickup address done and the payout done
    And exactly 1 ShopChanged event was queued

  @operation:updateMyShopProfile @FR-LOC-004
  Scenario: Errors come back in the request language
    When the user saves the shop profile in language "en" with name "ab" and no description
    Then the response status is 400
    And the error message is "Some fields are not valid. Check the highlighted fields."

  @operation:updateMyShopProfile @FR-ROLE-003
  Scenario Outline: A buyer-only token is forbidden
    Given a signed-in buyer who has not registered a shop
    When the buyer calls <operation>
    Then the response status is 403
    And the error code is "common.forbidden"

    Examples:
      | operation                |
      | updateMyShopProfile      |
      | setMyShopPickupAddress   |
      | setMyShopPayout          |

  @operation:setMyShopPayout @FR-ROLE-004 @NFR-SEC-006
  Scenario: A spoofed user header or a shop id in the body never selects the shop
    Given another user owns a shop named "Tiệm Bún Bò"
    When the user saves the payout with a spoofed X-User-Id of the other user and a body shopId
    Then the response status is 200
    And the other user's shop has no payout

  @operation:updateMyShopProfile @FR-AUTH-015
  Scenario Outline: Calls without a valid token are rejected
    When a caller saves shop settings with <credential>
    Then the response status is 401
    And the error code is "common.unauthenticated"

    Examples:
      | credential             |
      | no token               |
      | a tampered token       |
      | a wrong-audience token |
