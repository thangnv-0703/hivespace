@service:identity
Feature: Verify an email address
  The link in the verification email confirms ownership of the address (UC-001).

  @operation:verifyEmail @FR-AUTH-005
  Scenario: A valid token verifies the email
    Given a registered user with a pending verification token
    When the visitor verifies with that token
    Then the response status is 204

  @operation:verifyEmail @FR-AUTH-006
  Scenario: A never-issued token is invalid
    When the visitor verifies with token "never-issued"
    Then the response status is 400
    And the error code is "identity.verification_token_invalid"

  @operation:verifyEmail @FR-AUTH-006
  Scenario: An expired token is reported as expired
    Given a registered user with an expired verification token
    When the visitor verifies with that token
    Then the response status is 400
    And the error code is "identity.verification_token_expired"

  @operation:verifyEmail @FR-AUTH-006
  Scenario: A reused token reports the email as already verified
    Given a registered user whose email is verified with a token
    When the visitor verifies with that token
    Then the response status is 409
    And the error code is "identity.email_already_verified"
