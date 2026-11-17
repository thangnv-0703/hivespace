@service:catalog
Feature: List categories
  The walking skeleton's round trip: request in, Catalog reads Postgres, response out.

  @operation:listCategories @FR-LOC-007
  Scenario: Categories are returned with Vietnamese and English names
    When a client requests the category list
    Then the response status is 200
    And the response contains a category with slug "thoi-trang"
    And that category has a Vietnamese name and an English name
