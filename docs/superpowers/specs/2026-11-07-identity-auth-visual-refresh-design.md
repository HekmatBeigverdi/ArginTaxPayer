# Identity Sign In and Registration Visual Refresh

## Intent

Give the sign in, registration, and password recovery screens a polished, trustworthy visual identity that matches Argin's green application theme. Preserve all existing authentication behavior and routes.

## Goals

- Replace the single narrow centered card with the approved two-panel desktop composition: form on the right and a conceptual SVG illustration panel on the left.
- Provide distinct inline SVG artwork for sign in, registration, and password recovery, using the existing green palette with restrained dark teal/navy accents.
- Keep the composition responsive: stack the artwork and form cleanly on small screens without hiding the form or its controls.
- Preserve password, SMS OTP, email test registration, password reset, MFA, error, countdown, and restricted-login flows without changing API behavior.
- Respect reduced-motion preferences and keep decorative artwork out of the accessibility tree.

## Non-goals

- No authentication API, route, validation, or policy changes.
- No external image assets or new design dependencies; illustrations are native inline SVG.
- No separate routes for the three states; the existing state-driven identity flow remains in place.

## Approaches considered

1. **Two-panel responsive composition — selected by the user.** Keep the existing flow and place it beside a state-aware illustration. This gives the screens a stronger visual identity while retaining the current authentication state machine.
2. **Centered card with a small illustration above the form.** This would require fewer layout changes, but leaves the current narrow, generic composition largely intact.
3. **Separate routes for sign in, registration, and recovery.** This would create more distinct pages but would split the existing shared OTP/MFA state and make the current flow harder to preserve.

## Design decisions

- The existing identity page remains the route and owns the brand and security note; a small client `IdentityScreen` owns the responsive two-panel shell.
- The current `IdentityFlow` remains the source of truth for authentication state. It reports a visual state (`login`, `register`, or `recovery`) to the shell while retaining its existing internal OTP and MFA steps.
- Artwork is built from inline SVG shapes: a secure credential/document motif for sign in, a new-account/profile motif for registration, and a key/shield motif for recovery. Each uses the Argin green palette and a subtle geometric backdrop.
- Motion is limited to small decorative transitions and is disabled when `prefers-reduced-motion` is enabled.
- Existing form labels, messages, buttons, and security notices remain available and readable at every viewport size.

## Implementation scope

- `apps/web/app/identity/page.tsx`: preserve the server route, brand, restriction flag, and security note.
- `apps/web/app/identity/identity-screen.tsx`: compose the client shell, form panel, and state-aware inline SVG illustrations.
- `apps/web/app/identity/identity-flow.tsx`: report the current visual state without changing authentication requests or transitions.
- `apps/web/app/globals.css`: add responsive identity-only layout, green visual panel, SVG treatment, and motion/accessibility rules.

## Acceptance criteria

- Sign in, registration, and recovery each show their own conceptual SVG illustration.
- Existing login, registration, OTP, reset, and MFA interactions keep their current behavior.
- Desktop displays a balanced two-panel composition; mobile stacks it without horizontal overflow.
- The visual style uses the existing green Argin identity and remains usable with reduced motion and keyboard navigation.
