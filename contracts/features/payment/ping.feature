@service:payment
Feature: Payment answers
  Template sample. Replaced by the service's first feature slice.

  @operation:pingPayment
  Scenario: The service answers ping
    When a client calls GET /v1/payment/ping
    Then the response status is 200
