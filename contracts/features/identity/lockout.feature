@service:identity
Feature: Sign-in lockout
  Five consecutive wrong passwords for one account within 15 minutes lock it for 15 minutes (FR-AUTH-011). A locked
  account rejects every password attempt, even the right one (FR-AUTH-012).

  @operation:signIn @FR-AUTH-011 @UC-002
  Scenario: The fifth wrong password locks the account
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in wrongly 4 times as "an@example.com"
    Then the response status is 401
    And the error code is "identity.invalid_credentials"
    When the visitor signs in with "an@example.com" and "wrong9999"
    Then the response status is 423
    And the error code is "identity.account_locked"
    And the account is locked for 15 minutes
    And an "account_locked" audit entry exists for the account

  @operation:signIn @FR-AUTH-012 @UC-002
  Scenario: A locked account rejects the correct password
    Given a registered user with email "an@example.com" and password "hello1234"
    And the account is locked
    When the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 423
    And the error code is "identity.account_locked"
    And no Identity session cookie is set by the response

  @operation:signIn @FR-AUTH-012
  Scenario: The correct password works again after the lock ends
    Given a registered user with email "an@example.com" and password "hello1234"
    And the account is locked
    And the lock has ended
    When the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 200
    And the account has no lockout record

  @operation:signIn @FR-AUTH-011
  Scenario: A successful sign-in resets the count
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in wrongly 3 times as "an@example.com"
    And the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 200
    When the visitor signs in wrongly 4 times as "an@example.com"
    Then the response status is 401

  @operation:signIn @FR-AUTH-010
  Scenario: An unknown email is never locked
    When the visitor signs in wrongly 7 times as "nobody@example.com"
    Then the response status is 401
    And the error code is "identity.invalid_credentials"
    And no lockout record exists

  @operation:signIn @FR-AUTH-026
  Scenario: An account without a password is locked like any other
    Given a registered user with email "an@example.com" and password "hello1234"
    And the account has no password
    When the visitor signs in wrongly 5 times as "an@example.com"
    Then the response status is 423
    And the error code is "identity.account_locked"

  @operation:signIn @FR-LOC-004
  Scenario: The locked message comes back in the request language
    Given a registered user with email "an@example.com" and password "hello1234"
    And the account is locked
    When the visitor signs in in language "vi" with "an@example.com" and "hello1234"
    Then the response status is 423
    And the error message is "Bạn đã nhập sai quá nhiều lần. Tài khoản bị khóa 15 phút. Hãy thử lại sau hoặc đăng nhập bằng Google."

  @operation:signIn @NFR-SEC-009
  Scenario: Sign-ins are audited without personal data
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in with "an@example.com" and "wrong9999"
    And the visitor signs in with "an@example.com" and "hello1234"
    Then the audit has a "sign_in_failed" entry and a "sign_in_succeeded" entry for the account
    And no audit entry contains the email "an@example.com"
