# Project Engineering & Business Instructions

You are working on a real-world Training Center SaaS.

Your responsibility is not only to produce working code, but to preserve:

1. The actual business model.
2. The defined MVP scope.
3. The existing architecture and design decisions.
4. Security and tenant isolation.
5. Simplicity and maintainability.

==================================================

1. # MVP SCOPE — STRICT RULE

The current goal is the MVP.

The MVP core is:

- Authentication
- Organizations
- Users & Roles
- Students
- Instructors
- Courses
- Course Groups
- Enrollment
- Sessions
- Attendance
- Basic Dashboard

Do NOT add features outside the current MVP scope unless explicitly requested.

Do NOT add:

- Extra modules
- Extra workflows
- Extra dashboards
- Extra user roles
- Extra permissions
- Extra entities
- Extra notifications
- Extra integrations
- Extra settings
- Extra business rules

Do not implement functionality simply because it may be useful in the future.

Future ideas must remain outside the implementation unless explicitly requested.

Follow YAGNI:
"You aren't gonna need it."

================================================== 2. BUSINESS FIRST
==================================================

The system must represent a real Training Center business.

Do not design features as isolated technical CRUD operations.

Every feature must have a real business purpose.

Before implementing a feature, determine:

- What real business problem does it solve?
- Which actor uses it?
- Why does the actor need it?
- What business rule governs it?
- What is the minimum behavior required for the MVP?

Do not invent business behavior just to make the system look more complete.

Prefer:
Core business workflow > number of features.

A smaller system that correctly represents the business is better than a larger system full of unnecessary functionality.

================================================== 3. DO NOT INVENT BUSINESS RULES
==================================================

Do not assume business rules that were not defined.

Do not create:

- automatic behaviors
- permissions
- statuses
- calculations
- workflows
- restrictions
- notifications
- validations

unless they are justified by the existing business requirements.

When a technical implementation requires a business decision that is not defined:

- keep the implementation minimal
- preserve existing behavior
- do not invent a new business rule
- document the assumption when necessary

================================================== 4. SOLID PRINCIPLES
==================================================

Follow SOLID principles.

Single Responsibility:
Each class/module should have one clear responsibility.

Open/Closed:
Prefer extending behavior without modifying unrelated existing behavior.

Liskov Substitution:
Do not create abstractions that violate expected contracts.

Interface Segregation:
Prefer small focused interfaces over large generic interfaces.

Dependency Inversion:
High-level business logic must not depend directly on infrastructure details.

Do not apply SOLID mechanically.

Do NOT create abstractions simply to "follow SOLID."

Use abstractions when they provide a real architectural or business benefit.

================================================== 5. DESIGN PATTERNS
==================================================

Follow the architecture and design patterns already established in the project.

Do not introduce a new design pattern unless:

- it solves an actual problem
- it fits the existing architecture
- it reduces complexity rather than increasing it

Do not use design patterns for demonstration purposes.

Avoid pattern overuse.

Examples:
Do not create repositories, factories, strategies, mediators, handlers, managers, providers, or wrappers without a real reason.

Prefer simple code when simple code is sufficient.

================================================== 6. KISS / YAGNI / DRY
==================================================

Follow:

- KISS
- YAGNI
- DRY

However, do not force DRY too early.

A small amount of duplication is acceptable when abstraction would make the code harder to understand.

Avoid:

- premature abstraction
- speculative flexibility
- over-engineering
- unnecessary generic systems
- unnecessary configuration
- unnecessary helper layers

The simplest solution that correctly satisfies the business requirement is preferred.

================================================== 7. ARCHITECTURE
==================================================

The project uses a Modular Monolith with a simple Clean Architecture approach.

Respect the existing separation:

API
Application
Domain
Infrastructure

Controllers:

- HTTP concerns only
- No business logic
- No database logic

Application:

- Use cases
- Business workflows
- DTOs
- Validation
- Interfaces

Domain:

- Core business concepts
- Entities
- Enums
- Business rules

Infrastructure:

- Database
- EF Core
- Identity
- Authentication infrastructure
- External services

Do not bypass these boundaries without a strong reason.

================================================== 8. MULTI-TENANCY
==================================================

The system is multi-tenant.

Each Organization is isolated from every other Organization.

Never allow cross-tenant access.

Organization context must come from the authenticated user's current organization context.

Do NOT trust OrganizationId supplied by the client for authorization.

Every tenant-owned operation must verify:

- current user
- current organization
- resource ownership
- permission

Never assume that because an entity ID exists, the current user can access it.

Cross-tenant data access must be explicitly prevented.

================================================== 9. AUTHORIZATION & ROLES
==================================================

Follow the principle of least privilege.

Do NOT create a role unless the real business requires it.

Do NOT create permissions merely because they might be useful later.

Do NOT give users broader permissions than required.

Every role must have:

- a real business purpose
- clearly defined responsibilities
- minimum required permissions

Never solve a missing business requirement by creating a new role automatically.

Prefer fewer roles with clear responsibilities.

Do not duplicate authorization logic throughout the codebase.

Authorization must be centralized and consistent.

================================================== 10. SECURITY
==================================================

Security is part of the feature, not an optional improvement.

For every protected operation consider:

- Authentication
- Authorization
- Organization isolation
- Input validation
- Resource ownership
- Sensitive data exposure
- Mass assignment
- Insecure direct object references
- Overly permissive endpoints

Never expose internal entities directly when a DTO is appropriate.

Never trust client-provided authorization data.

Do not expose:

- passwords
- password hashes
- refresh tokens
- security secrets
- internal implementation details
- sensitive tenant data

Use secure defaults.

Default to deny rather than allow.

================================================== 11. DATABASE
==================================================

Design the database around real business relationships.

Do not create a table because:

- it "might be useful later"
- it makes the architecture look more complete
- it could support a future feature

Every relationship must have a business reason.

Use:

- primary keys
- foreign keys
- unique constraints
- appropriate indexes
- correct delete behavior

Protect business integrity at the database and application levels where appropriate.

================================================== 12. API DESIGN
==================================================

APIs must represent real business operations.

Do not create endpoints simply because CRUD is possible.

Prefer meaningful resource-oriented REST APIs.

Use:

- correct HTTP methods
- correct HTTP status codes
- DTOs
- validation
- pagination where needed
- consistent error responses

Do not expose internal implementation details through the API.

================================================== 13. FRONTEND
==================================================

The frontend must represent the same business model as the backend.

Do not add UI features that have no business requirement.

Do not add unnecessary:

- filters
- buttons
- settings
- dashboards
- actions
- forms
- pages

Every visible action must have a clear business purpose.

Do not create UI controls for backend functionality that is intentionally outside the MVP.

================================================== 14. FEATURE IMPLEMENTATION
==================================================

When implementing a feature:

1. Understand the business requirement.
2. Inspect the existing architecture.
3. Reuse existing patterns and conventions.
4. Identify the minimum required behavior.
5. Implement only that behavior.
6. Add required validation and authorization.
7. Preserve tenant isolation.
8. Add tests for important business/security rules.
9. Avoid unrelated refactoring.
10. Avoid unrelated features.

Do not use a feature request as an excuse to redesign unrelated parts of the system.

================================================== 15. SCOPE CONTROL
==================================================

Never expand scope silently.

If you discover a potentially useful feature while implementing another feature:

DO NOT implement it automatically.

Treat it as a future backlog item instead.

Examples:

- Notifications
- Audit logs
- Advanced analytics
- Payments
- Subscriptions
- File management
- AI
- Calendar integrations
- External integrations

These must remain out of the MVP unless explicitly requested.

================================================== 16. REFACTORING
==================================================

Refactoring is allowed only when it directly improves the current task or fixes a real architectural problem.

Do not perform large unrelated refactors.

Do not rename or restructure large parts of the project without a clear reason.

Avoid "cleaning up" working code merely according to personal preference.

Preserve established project conventions unless they are clearly harmful.

================================================== 17. AI-SPECIFIC RULE
==================================================

Do not generate unnecessary complexity just because you are capable of generating it.

Do not optimize for:

- number of files
- number of classes
- number of patterns
- number of abstractions
- number of features

Optimize for:

- correctness
- business value
- security
- maintainability
- simplicity

Do not confuse "more code" with "better software."

================================================== 18. DEFINITION OF DONE
==================================================

A feature is not complete simply because the code compiles.

A feature is Done when:

- Business requirement is satisfied.
- MVP scope is respected.
- Authorization is correct.
- Tenant isolation is preserved.
- Validation exists where required.
- Database integrity is preserved.
- Important business/security rules are tested.
- Existing architecture is respected.
- No unnecessary feature was added.
- No unrelated behavior was changed.

================================================== 19. FINAL CHECK BEFORE COMPLETING ANY TASK
==================================================

Before finishing any task, verify:

[ ] Does this solve a real business requirement?
[ ] Is this inside the current MVP scope?
[ ] Did I add anything that was not requested?
[ ] Did I create any unnecessary role or permission?
[ ] Did I introduce unnecessary abstraction?
[ ] Did I introduce a design pattern without a real need?
[ ] Is tenant isolation preserved?
[ ] Is authorization least-privilege?
[ ] Could this be implemented more simply?
[ ] Did I accidentally implement a future feature?
[ ] Did I modify unrelated parts of the system?

If any unnecessary functionality was added, remove it before finishing.

================================================== 20. CORE PRINCIPLE
==================================================

Build the smallest system that correctly solves the real business problem.

Business correctness > feature count.

Security > convenience.

Clarity > abstraction.

Simplicity > cleverness.

MVP scope > future possibilities.

Do not build for hypothetical requirements.

Do not add functionality just because it is technically possible.
