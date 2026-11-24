@service:identity
Feature: Register a shop and become a seller
  A signed-in buyer with a verified email registers a shop and becomes a seller (UC-003).

  @operation:registerShop @FR-SHOP-001 @FR-ROLE-002
  Scenario: A verified buyer registers a shop and becomes a seller
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop named "Tiệm Bánh An" of type "individual"
    Then the response status is 201
    And the location is "/v1/shops/me"
    And the shop has name "Tiệm Bánh An", type "individual" and an unfinished setup
    And the user has the roles "Buyer" and "Seller"
    And exactly 1 ShopRegistered event was queued

  @operation:registerShop @FR-SHOP-004
  Scenario Outline: Both seller types are accepted and returned as submitted
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop named "Tiệm Bánh An" of type "<type>"
    Then the response status is 201
    And the shop has name "Tiệm Bánh An", type "<type>" and an unfinished setup

    Examples:
      | type       |
      | individual |
      | business   |

  @operation:registerShop @FR-SHOP-008
  Scenario: Spaces around and inside the name are trimmed and collapsed
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop named "  Tiệm   Bánh  An " of type "individual"
    Then the response status is 201
    And the shop has name "Tiệm Bánh An", type "individual" and an unfinished setup

  @operation:registerShop @FR-SHOP-008
  Scenario: Every invalid field is reported in one response and nothing is saved
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop named "ab" of type "x"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "shop_name_too_short"
    And the details include "seller_type_invalid"
    And the user has the roles "Buyer"
    And exactly 0 ShopRegistered event was queued

  @operation:registerShop @FR-SHOP-008
  Scenario Outline: Name rules
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop with a name of <length> characters of type "individual"
    Then the response status is <status>

    Examples:
      | length | status |
      | 2      | 400    |
      | 3      | 201    |
      | 60     | 201    |
      | 61     | 400    |

  @operation:registerShop @FR-SHOP-008
  Scenario: A missing or control-character name is rejected
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop with no name and no type
    Then the response status is 400
    And the details include "shop_name_required"
    And the details include "seller_type_invalid"
    When the user registers a shop named with a control character of type "individual"
    Then the response status is 400
    And the details include "shop_name_invalid"

  @operation:registerShop @FR-SHOP-008
  Scenario: An unknown key is a field error
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop with an extra key "ownerId"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "ownerId"

  @operation:registerShop @FR-SHOP-001
  Scenario: An unverified email cannot register a shop
    Given a signed-in user with an unverified email "an@example.com"
    When the user registers a shop named "Tiệm Bánh An" of type "individual"
    Then the response status is 403
    And the error code is "identity.email_not_verified"
    And the user has the roles "Buyer"

  @operation:registerShop @FR-SHOP-002
  Scenario: One shop per user
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop named "Tiệm Bánh An" of type "individual"
    And the user registers a shop named "Another Shop" of type "business"
    Then the response status is 409
    And the error code is "identity.shop_already_owned"

  @operation:registerShop @FR-SHOP-003
  Scenario: Shop names are unique ignoring case and spacing
    Given a signed-in user with a verified email "an@example.com"
    And another user owns a shop named "Tiệm Bánh An"
    When the user registers a shop named "tiệm  bánh an " of type "individual"
    Then the response status is 409
    And the error code is "identity.shop_name_taken"
    And the user has the roles "Buyer"
    And exactly 1 ShopRegistered event was queued

  @operation:registerShop @FR-SHOP-003
  Scenario: Names that differ by an accent are different names
    Given a signed-in user with a verified email "an@example.com"
    And another user owns a shop named "Tiệm Bánh An"
    When the user registers a shop named "Tiem Banh An" of type "individual"
    Then the response status is 201

  @operation:registerShop @FR-LOC-004
  Scenario: Errors come back in the request language
    Given a signed-in user with a verified email "an@example.com"
    When the user registers a shop in language "en" named "ab" of type "individual"
    Then the response status is 400
    And the error message is "Some fields are not valid. Check the highlighted fields."

  @operation:registerShop @FR-AUTH-015 @FR-ROLE-004
  Scenario Outline: Calls without a valid token are rejected
    When a caller registers a shop with <credential>
    Then the response status is 401
    And the error code is "common.unauthenticated"

    Examples:
      | credential             |
      | no token               |
      | a tampered token       |
      | a wrong-audience token |

  @operation:getMyShop @FR-ROLE-003
  Scenario: A seller reads their own shop
    Given a signed-in user with a verified email "an@example.com"
    And the user registered a shop named "Tiệm Bánh An" of type "business" and refreshed the token
    When the user reads the shop
    Then the response status is 200
    And the shop has name "Tiệm Bánh An", type "business" and an unfinished setup

  @operation:getMyShop @FR-ROLE-003
  Scenario: A buyer-only token is forbidden
    Given a signed-in user with a verified email "an@example.com"
    When the user reads the shop
    Then the response status is 403
    And the error code is "common.forbidden"

  @operation:getMyShop @FR-ROLE-004 @NFR-SEC-006
  Scenario: A spoofed user header changes nothing
    Given a signed-in user with a verified email "an@example.com"
    And another user owns a shop named "Tiệm Bánh An"
    When the user registers a shop named "My Own Shop" of type "individual" with a spoofed X-User-Id of the other user
    Then the response status is 201
    And the shop is owned by the signed-in user
    And the user registered a shop and refreshed the token
    When the user reads the shop with a spoofed X-User-Id of the other user
    Then the response status is 200
    And the shop has name "My Own Shop", type "individual" and an unfinished setup

  @operation:getMyShop @FR-AUTH-015
  Scenario Outline: Reading the shop without a valid token is rejected
    When a caller reads the shop with <credential>
    Then the response status is 401
    And the error code is "common.unauthenticated"

    Examples:
      | credential             |
      | no token               |
      | a tampered token       |
      | a wrong-audience token |
