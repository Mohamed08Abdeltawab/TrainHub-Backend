# Project Engineering & Business Instructions

This repository is BACKEND ONLY.

The backend is a real-world Training Center SaaS built with:
- ASP.NET Core Web API
- C#
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- JWT Authentication
- RESTful API

Architecture:
- Modular Monolith
- Simple Clean Architecture

==================================================
1. MVP SCOPE
==================================================

Current MVP:

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

Do not implement features outside the MVP unless explicitly requested.

Do not add:
- Payments
- Subscriptions
- Notifications
- Files
- AI
- Advanced Analytics
- External Integrations
- Extra Roles
- Extra Permissions
- Extra Entities
- Extra Workflows

Follow YAGNI.

==================================================
2. BUSINESS FIRST
==================================================

The system must model a real Training Center business.

Every feature must have a real business purpose.

Do not add functionality simply because:
- it is technically possible
- it may be useful later
- it is common in other systems

Core business workflow is more important than feature count.

Do not invent business rules that were not defined.

When a business decision is unclear:
- keep the implementation minimal
- preserve existing behavior
- do not invent new behavior

==================================================
3. ARCHITECTURE
==================================================

Respect the existing architecture:

API
Application
Domain
Infrastructure

API:
- HTTP concerns only
- Controllers
- Authentication/Authorization configuration
- Middleware
- OpenAPI

Application:
- Use cases
- Business workflows
- DTOs
- Validation
- Interfaces

Domain:
- Entities
- Enums
- Core business rules
- Domain invariants

Infrastructure:
- EF Core
- SQL Server
- Identity
- Authentication infrastructure
- External services

Do not bypass these boundaries without a real architectural reason.

Do not introduce Microservices.

==================================================
4. SOLID
==================================================

Follow SOLID principles.

Apply SOLID pragmatically.

Do not create abstractions just to satisfy SOLID.

Prefer:
- clear responsibilities
- focused interfaces
- dependency inversion
- maintainable code

Avoid unnecessary:
- repositories
- factories
- strategies
- managers
- providers
- wrappers
- generic abstractions

==================================================
5. DESIGN PATTERNS
==================================================

Use the existing project patterns consistently.

Introduce a new pattern only when:
- it solves a real problem
- it fits the existing architecture
- it reduces complexity

Do not use patterns for demonstration purposes.

Prefer simple code when simple code is sufficient.

==================================================
6. KISS / YAGNI / DRY
==================================================

Follow:
- KISS
- YAGNI
- DRY

Avoid:
- premature abstraction
- speculative flexibility
- over-engineering
- unnecessary configuration
- unnecessary layers
- unnecessary generic systems

A small amount of duplication is acceptable when abstraction would add complexity.

==================================================
7. MULTI-TENANCY
==================================================

The system is multi-tenant.

Every Training Center is an Organization.

Organization data must remain isolated.

Never allow cross-tenant access.

Organization context must come from the authenticated user / trusted server-side context.

Never trust client-supplied OrganizationId for authorization.

Every tenant-owned operation must verify:
- current organization
- resource ownership
- authorization

All tenant queries must respect tenant isolation.

Cross-tenant reads, updates, deletes, and creates must be prevented.

==================================================
8. USERS & ORGANIZATIONS
==================================================

Every ApplicationUser must have a non-null OrganizationId.

The SaaS Platform has a dedicated Platform Organization.

SuperAdmin users belong to the Platform Organization.

Training Center users belong to their Training Center Organization.

Current roles are only:

- SuperAdmin
- OrgAdmin
- Instructor
- Student

Do not create additional roles without an explicit business requirement.

Roles must follow least privilege.

==================================================
9. AUTHORIZATION
==================================================

Authorization must represent real business responsibilities.

SuperAdmin:
- Platform-level administration
- Organization management
- SaaS administration

OrgAdmin:
- Manage their Training Center

Instructor:
- Access instructor-specific functionality

Student:
- Access student-specific functionality

Never grant broader permissions than required.

Do not use roles as a substitute for resource ownership checks.

Always combine:
Authentication
+
Authorization
+
Organization isolation
+
Resource ownership

==================================================
10. SECURITY
==================================================

Security is mandatory for every feature.

Consider:
- Authentication
- Authorization
- Tenant isolation
- Input validation
- Resource ownership
- Mass assignment
- IDOR
- Sensitive data exposure
- Overly permissive endpoints

Never trust client authorization data.

Never expose:
- Passwords
- Password hashes
- Refresh tokens
- Secrets
- Internal database details
- Sensitive tenant data
- Stack traces in production

Use secure defaults.

Default to deny.

==================================================
11. DATABASE
==================================================

Use SQL Server + EF Core.

Design the database around real business relationships.

Use:
- Primary keys
- Foreign keys
- Unique constraints
- Appropriate indexes
- Correct delete behaviors

Do not create tables or columns for hypothetical future features.

Protect business integrity at both application and database levels where appropriate.

==================================================
12. IDENTITY
==================================================

Use ASP.NET Core Identity for authentication and password security.

Do not implement custom password hashing.

Users belong to exactly one Organization.

The same email may exist in multiple Organizations.

Email uniqueness must be scoped to the Organization.

Conceptually:

UNIQUE(OrganizationId, NormalizedEmail)

not globally:

UNIQUE(NormalizedEmail)

==================================================
13. API DESIGN
==================================================

APIs must represent real business operations.

Prefer resource-oriented REST APIs.

Use:
- Correct HTTP methods
- Correct HTTP status codes
- DTOs
- Validation
- Pagination where needed
- Consistent error responses
- OpenAPI documentation

Do not expose internal entities directly when DTOs are appropriate.

Do not create endpoints only because a CRUD endpoint is technically possible.

==================================================
14. VALIDATION
==================================================

Validation should have clear responsibility.

Boundary validation:
- DTO validation
- Required fields
- Format
- Basic input constraints

Domain validation:
- Business invariants

Application validation:
- Database-dependent rules
- Business relationships
- Uniqueness
- Resource existence

Do not duplicate the same rule unnecessarily across multiple layers.

Do not weaken domain invariants when removing duplicate validation.

==================================================
15. EXCEPTIONS & ERROR HANDLING
==================================================

Use centralized exception handling.

Return safe API errors.

Do not expose:
- SQL exception messages
- Stack traces
- Internal class names
- Database schema details

Log detailed diagnostics server-side.

==================================================
16. TESTING
==================================================

Tests must protect business behavior and security.

Prioritize:
- Authentication
- Authorization
- Tenant isolation
- Business rules
- Important API flows
- Database integrity

Do not keep tests for dead or removed code.

Do not remove meaningful security or business coverage merely to reduce test count.

==================================================
17. FEATURE IMPLEMENTATION
==================================================

Before implementing a feature:

1. Understand the business requirement.
2. Inspect the existing architecture.
3. Reuse existing conventions.
4. Implement the minimum required behavior.
5. Add required validation.
6. Add authorization.
7. Preserve tenant isolation.
8. Add appropriate tests.
9. Avoid unrelated refactoring.
10. Avoid future features.

==================================================
18. SCOPE CONTROL
==================================================

Never silently expand the MVP.

If you discover a useful future feature:
- Do not implement it.
- Treat it as future backlog work.

Do not turn one feature into a larger system.

==================================================
19. REFACTORING
==================================================

Refactor only when:
- fixing a real architectural problem
- improving the current task
- removing unnecessary complexity

Do not perform unrelated refactoring.

Do not restructure working code based only on personal preference.

==================================================
20. DEFINITION OF DONE
==================================================

A backend feature is Done when:

- The real business requirement is satisfied.
- MVP scope is respected.
- API behavior is correct.
- Authorization is correct.
- Tenant isolation is preserved.
- Validation is appropriate.
- Database integrity is preserved.
- Important tests pass.
- Existing architecture is respected.
- No unnecessary role or feature was added.
- No unrelated behavior was changed.

==================================================
21. AI RULE
==================================================

Do not generate complexity simply because it is possible.

Optimize for:

- Business correctness
- Security
- Maintainability
- Simplicity
- Correct architecture

Do not optimize for:

- Number of files
- Number of classes
- Number of abstractions
- Number of design patterns
- Number of features

More code does not mean better software.

==================================================
22. CORE PRINCIPLE
==================================================

Build the smallest backend that correctly solves the real business problem.

Business correctness > feature count

Security > convenience

Clarity > abstraction

Simplicity > cleverness

MVP scope > future possibilities

Do not build for hypothetical requirements.