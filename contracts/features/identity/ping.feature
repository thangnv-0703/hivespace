@service:identity
Feature: Identity answers
  Template sample. Replaced by the service's first feature slice.

  @operation:pingIdentity
  Scenario: The service answers ping
    When a client calls GET /v1/identity/ping
    Then the response status is 200
