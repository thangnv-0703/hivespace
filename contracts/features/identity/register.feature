@service:identity
Feature: Register an account
  A visitor creates a buyer account and receives a verification email (UC-001).

  @operation:registerUser @FR-AUTH-001 @FR-ROLE-001
  Scenario: A visitor registers with valid details
    When a visitor registers with email "an@example.com", name "An Nguyen" and password "hello1234"
    Then the response status is 201
    And the response contains a user id

  @operation:registerUser @FR-AUTH-002
  Scenario: Registering an existing email is rejected regardless of case
    Given a registered user with email "an@example.com"
    When a visitor registers with email "AN@Example.com", name "Other" and password "hello1234"
    Then the response status is 409
    And the error code is "identity.email_in_use"

  @operation:registerUser @FR-AUTH-003
  Scenario: A weak password lists every unmet rule
    When a visitor registers with email "an@example.com", name "An Nguyen" and password "abc"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the details include "password_too_short"
    And the details include "password_needs_digit"

  @operation:registerUser @FR-AUTH-004
  Scenario: Missing and malformed fields are each reported
    When a visitor registers with email "a@", name "" and password "hello1234"
    Then the response status is 400
    And the details include "email_invalid"
    And the details include "display_name_required"

  @operation:registerUser @FR-LOC-004
  Scenario Outline: Messages follow Accept-Language while codes stay the same
    When a visitor registers in language "<lang>" with email "a@", name "An" and password "hello1234"
    Then the response status is 400
    And the error code is "common.validation_failed"
    And the error message is "<message>"

    Examples:
      | lang | message                                                          |
      | en   | Some fields are not valid. Check the highlighted fields.         |
      | vi   | Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường được đánh dấu. |
