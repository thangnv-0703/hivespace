@service:media
Feature: Media answers
  Template sample. Replaced by the service's first feature slice.

  @operation:pingMedia
  Scenario: The service answers ping
    When a client calls GET /v1/media/ping
    Then the response status is 200
