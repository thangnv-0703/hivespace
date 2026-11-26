@service:catalog
Feature: Category tree
  Shoppers and sellers read the category tree one level at a time and a leaf's attributes (FR-CAT-003, FR-CAT-020, FR-CAT-021, FR-LOC-007).
  The tree is loaded by an operator command; these scenarios start from a known loaded tree.

  Background:
    Given a loaded category tree

  @operation:listCategories @FR-CAT-003 @FR-LOC-007 @UC-005 @UC-007
  Scenario: Root categories are returned with Vietnamese and English names
    When a client requests the root categories
    Then the response status is 200
    And every category has a Vietnamese name and an English name
    And the category ids are "1789,8322,1686"

  @operation:listCategories @FR-CAT-003 @UC-005
  Scenario: Children come back in file order whatever the language
    When a client requests the children of category 8061 in "en"
    Then the response status is 200
    And the category ids are "8062,8063"
    When a client requests the children of category 8061 in "vi"
    Then the category ids are "8062,8063"

  @operation:listCategories @FR-CAT-003
  Scenario: A leaf has no children
    When a client requests the children of category 1795
    Then the response status is 200
    And there are no categories
    When a client requests the root categories
    Then the category "1789" is not a leaf
    When a client requests the children of category 1789
    Then the category "1795" is a leaf

  @operation:listCategories @FR-CAT-003
  Scenario: An unknown parent is not found
    When a client requests the children of category 999999
    Then the response status is 404
    And the error code is "common.not_found"

  @operation:listCategories @FR-CAT-003
  Scenario: A malformed parent is not found
    When a client requests the children of category "abc"
    Then the response status is 404
    And the error code is "common.not_found"

  @operation:getCategory @FR-CAT-020 @UC-005
  Scenario: A leaf shows its list attributes in file order
    When a client requests category 1795
    Then the response status is 200
    And the category path is "1789"
    And the attribute "1001" is a required single list with maximum 1 and values "5001,5002,5003,5004"
    And the attribute "1003" is an optional multiple list with maximum 3 and values "5201,5202,5203,5204"

  @operation:getCategory @FR-CAT-020
  Scenario: A text attribute has no values
    When a client requests category 8062
    Then the response status is 200
    And the attribute "1005" is an optional text attribute

  @operation:getCategory @FR-CAT-020 @FR-LOC-007
  Scenario: A value with the same Vietnamese and English name is returned as is
    When a client requests category 1795
    Then the value "5001" of attribute "1001" is named "Apple" in both languages

  @operation:getCategory @FR-CAT-020
  Scenario: A leaf with no attributes returns an empty list
    When a client requests category 1883
    Then the response status is 200
    And the category has no attributes

  @operation:getCategory @FR-CAT-020
  Scenario: A non-leaf category shows no attributes
    When a client requests category 8061
    Then the response status is 200
    And the category has no attributes

  @operation:listCategories @operation:getCategory @FR-CAT-021 @UC-007
  Scenario: A root with an image returns it and a root without one returns null
    When a client requests the root categories
    Then the category "1789" has image "https://example.com/sample-images/categories/phones-tablets.jpg"
    And the category "1686" has no image

  @operation:listCategories @FR-CAT-021
  Scenario: Children have no image
    When a client requests the children of category 1789
    Then the category "1795" has no image

  @operation:getCategory @FR-CAT-003
  Scenario: An unknown category is not found
    When a client requests category 999999
    Then the response status is 404
    And the error code is "common.not_found"

  @operation:getCategory @FR-CAT-003
  Scenario: A malformed id is not found
    When a client requests category "abc"
    Then the response status is 404
    And the error code is "common.not_found"
