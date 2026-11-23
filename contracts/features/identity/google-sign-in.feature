@service:identity @UC-025
Feature: Sign in with Google
  Google sign-in is a chain of browser redirects through Identity's /connect/external/{provider}/* endpoints (FEAT-004
  technical design §3.1). They are not OpenAPI operations. The steps play the browser against the fake IdP in tools/fake-idp,
  which stands in for Google.

  @FR-AUTH-020 @NFR-SEC-014 @EIR-SW-006
  Scenario: Start redirects to Google with the authorization code flow and PKCE
    When the visitor starts Google sign-in with returnUrl "/connect/authorize?client_id=x"
    Then the response status is 302
    And the redirect goes to the Google authorize endpoint with
      | parameter             | value                                                |
      | response_type         | code                                                 |
      | code_challenge_method | S256                                                 |
      | scope                 | openid email profile                                 |
      | redirect_uri          | http://localhost:5080/connect/external/google/callback |
    And the redirect carries a "code_challenge", a "state" and a "nonce"

  @FR-AUTH-020
  Scenario: An unknown provider is not found
    When the visitor starts sign-in with provider "facebook"
    Then the response status is 404

  @FR-AUTH-020
  Scenario: A new Google identity signs in and comes back to the returnUrl
    Given Google will answer with a verified identity "an@gmail.com" named "An Nguyen"
    When the visitor signs in with Google with returnUrl "/connect/authorize?client_id=x"
    Then the browser ends at "/connect/authorize?client_id=x"
    And an Identity session cookie is set
    And exactly 1 ExternalLoginLinked event was queued

  @FR-AUTH-022
  Scenario Outline: A failed Google sign-in returns to the sign-in page with a stable code
    Given Google will answer with outcome "<outcome>"
    When the visitor signs in with Google with returnUrl "/connect/authorize?client_id=x"
    Then the browser ends at "/signin?error=<code>&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dx"
    And no Identity session cookie is set
    And exactly 0 ExternalLoginLinked event was queued

    Examples:
      | outcome       | code                                |
      | denied        | identity.external_login_denied      |
      | unverified    | identity.external_email_unverified  |
      | bad_signature | identity.external_login_failed      |

  @FR-AUTH-023 @NFR-REL-008
  Scenario: Google being down at the token call gives the unavailable code
    Given Google will answer with outcome "down"
    When the visitor signs in with Google with returnUrl "/connect/authorize?client_id=x"
    Then the browser ends at "/signin?error=identity.external_login_unavailable&returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dx"
    And no Identity session cookie is set

  @FR-AUTH-020
  Scenario Outline: Only a local authorize URL is kept as returnUrl
    Given Google will answer with outcome "<outcome>"
    When the visitor signs in with Google with returnUrl "<returnUrl>"
    Then the browser ends at "<end>"

    Examples:
      | outcome | returnUrl                              | end                                    |
      | ok      | https://evil.example/connect/authorize | /                                      |
      | ok      | /cart                                  | /                                      |
      | denied  | https://evil.example/connect/authorize | /signin?error=identity.external_login_denied |

  @FR-AUTH-026 @UC-002
  Scenario: An account created with Google has no password to sign in with
    Given Google will answer with a verified identity "an@gmail.com" named "An Nguyen"
    And the visitor signed in with Google
    When the visitor signs in with "an@gmail.com" and "hello1234"
    Then the response status is 401
    And the error code is "identity.invalid_credentials"
