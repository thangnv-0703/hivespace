@service:identity
Feature: Sign in with email and password

  @operation:signIn @FR-AUTH-009
  Scenario: Correct credentials sign in even when the email is unverified
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in with "an@example.com" and "hello1234"
    Then the response status is 200
    And the redirect URL is "/"
    And an Identity session cookie is set

  @operation:signIn @FR-AUTH-010
  Scenario Outline: Every failure cause gives the same answer
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in with "<email>" and "<password>"
    Then the response status is 401
    And the error code is "identity.invalid_credentials"

    Examples:
      | email              | password  |
      | an@example.com     | wrong9999 |
      | nobody@example.com | hello1234 |

  @operation:signIn @FR-AUTH-009
  Scenario Outline: Only a local authorize URL is honored as returnUrl
    Given a registered user with email "an@example.com" and password "hello1234"
    When the visitor signs in with "an@example.com" and "hello1234" and returnUrl "<returnUrl>"
    Then the response status is 200
    And the redirect URL is "<redirect>"

    Examples:
      | returnUrl                              | redirect                       |
      | /connect/authorize?client_id=x         | /connect/authorize?client_id=x |
      | https://evil.example/connect/authorize | /                              |
      | /cart                                  | /                              |
