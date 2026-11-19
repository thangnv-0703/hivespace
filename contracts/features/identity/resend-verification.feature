@service:identity
Feature: Resend the verification email

  @operation:resendVerificationEmail @FR-AUTH-007
  Scenario: An unverified account gets a new link and the old link stops working
    Given a registered user with a pending verification token
    When the visitor requests a new verification email for that user
    Then the response status is 202
    And the earlier token is reported as expired when used

  @operation:resendVerificationEmail @FR-AUTH-007
  Scenario: An unknown email gets the same answer and nothing is sent
    When the visitor requests a new verification email for "nobody@example.com"
    Then the response status is 202
    And no verification email event was queued

  @operation:resendVerificationEmail @FR-AUTH-007
  Scenario: A malformed email is rejected
    When the visitor requests a new verification email for "a@"
    Then the response status is 400
    And the error code is "common.validation_failed"
