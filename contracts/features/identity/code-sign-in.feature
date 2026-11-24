@service:identity
Feature: Sign in with an email code
  A visitor asks for a 6-digit code by email and types it in to sign in without a password (FR-AUTH-028, FR-AUTH-030).
  The answers never tell whether the email has an account (FR-AUTH-029, FR-AUTH-031).

  @operation:requestSignInCode @FR-AUTH-028 @UC-029
  Scenario: A registered email gets a code
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor requests a sign-in code for "an@example.com"
    Then the response status is 201
    And the response contains a challenge that expires in 5 minutes and allows a resend in 60 seconds
    And a sign-in code email event was queued for "an@example.com"

  @operation:requestSignInCode @FR-AUTH-029 @UC-029
  Scenario: An unknown email gets the same answer and nothing is sent
    When the visitor requests a sign-in code for "nobody@example.com"
    Then the response status is 201
    And the response contains a challenge that expires in 5 minutes and allows a resend in 60 seconds
    And no sign-in code email event was queued

  @operation:requestSignInCode @FR-AUTH-028
  Scenario: A malformed email is rejected
    When the visitor requests a sign-in code for "a@"
    Then the response status is 400
    And the error code is "common.validation_failed"

  @operation:requestSignInCode @FR-AUTH-033 @UC-029
  Scenario: A second request within 60 seconds is refused with the wait
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor requests a sign-in code for "an@example.com"
    And the visitor requests a sign-in code for "an@example.com"
    Then the response status is 429
    And the error code is "identity.sign_in_code_too_soon"
    And the response has a Retry-After header

  @operation:signInWithCode @FR-AUTH-030 @UC-029
  Scenario: The correct code signs in
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    When the visitor submits the emailed code with returnUrl "/connect/authorize?client_id=storefront"
    Then the response status is 200
    And the redirect URL is "/connect/authorize?client_id=storefront"
    And an Identity session cookie is set

  @operation:signInWithCode @FR-AUTH-030
  Scenario: A used code cannot be used again
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    When the visitor submits the emailed code
    And the visitor submits the emailed code
    Then the response status is 401
    And the error code is "identity.sign_in_challenge_ended"

  @operation:signInWithCode @FR-AUTH-031 @UC-029
  Scenario: A wrong code is rejected
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    When the visitor submits a wrong code
    Then the response status is 401
    And the error code is "identity.sign_in_code_invalid"
    And no Identity session cookie is set by the response

  @operation:signInWithCode @FR-AUTH-032 @UC-029
  Scenario: The fifth wrong code ends the code
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    When the visitor submits a wrong code 4 times
    Then the response status is 401
    And the error code is "identity.sign_in_code_invalid"
    When the visitor submits a wrong code
    Then the response status is 401
    And the error code is "identity.sign_in_challenge_ended"
    When the visitor submits the emailed code
    Then the response status is 401
    And the error code is "identity.sign_in_challenge_ended"

  @operation:signInWithCode @FR-AUTH-031 @UC-029
  Scenario: An expired code is rejected
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    And the code has expired
    When the visitor submits the emailed code
    Then the response status is 401
    And the error code is "identity.sign_in_code_expired"

  @operation:signInWithCode @FR-AUTH-034 @UC-029
  Scenario: A new code replaces the earlier one
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    And the resend wait has passed
    When the visitor requests a sign-in code for "an@example.com"
    And the visitor submits the earlier emailed code
    Then the response status is 401
    And the error code is "identity.sign_in_challenge_ended"

  @operation:signInWithCode @FR-AUTH-035 @UC-029
  Scenario: A locked account cannot sign in with a code
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    And the account is locked
    When the visitor submits the emailed code
    Then the response status is 423
    And the error code is "identity.account_locked"
    And no Identity session cookie is set by the response

  @operation:signInWithCode @FR-AUTH-029 @FR-AUTH-031
  Scenario: A code for an unknown email is rejected like any wrong code
    When the visitor requests a sign-in code for "nobody@example.com"
    And the visitor submits a wrong code
    Then the response status is 401
    And the error code is "identity.sign_in_code_invalid"

  @operation:signInWithCode @FR-AUTH-031
  Scenario: A code that is not six digits is rejected
    Given a registered user with email "an@example.com" and password "hello1234"
    And the visitor has requested a sign-in code for "an@example.com"
    When the visitor submits the code "12ab"
    Then the response status is 400
    And the error code is "common.validation_failed"
