@service:order
Feature: Order answers
  Template sample. Replaced by the service's first feature slice.

  @operation:pingOrder
  Scenario: The service answers ping
    When a client calls GET /v1/order/ping
    Then the response status is 200
