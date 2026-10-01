# Princess Dog Walker

A web application for a dog walking and pet-care business, built to help customers explore services, check availability, and request care while giving the business owner a central place to manage bookings.

**[Visit the web app](https://princessdogwalker.onrender.com/assistant.html)**

## About the project

I created Princess Dog Walker for my friend to support her growing pet-care business. The project brings service information, scheduling, customer accounts, and day-to-day booking management into one application.

This public repository contains a standalone, sanitized copy of the application for local demonstrations. The production application and its development history remain private.

## How it works

1. Visitors browse services and check availability.
2. Customers create an account, save their dogs, and select a service and available time. Overnight care uses check-in and checkout dates.
3. The application validates the request against the schedule before saving the booking.
4. The owner reviews requests and manages booking statuses, services, and schedule blocks.
5. Customers follow their booking status from their dashboard. Assistant / pet-sitter accounts support team workflows.

## Main features

- Service listings and availability calendars.
- Customer accounts, dog profiles, and booking requests.
- Regular appointments and overnight care.
- Owner tools for managing services, schedules, and booking statuses.
- Assistant / pet-sitter accounts for team workflows.

## Technology stack

| Layer | Technology |
| --- | --- |
| Frontend | HTML, CSS, and vanilla JavaScript |
| Backend | ASP.NET Core Web API (C#) |
| Data | PostgreSQL and Entity Framework Core |
| Accounts | ASP.NET Core Identity and Google sign-in |
| Deployment | Docker and Render |
| Tests | xUnit |

## Application structure

```text
Browser interface
        |
        v
ASP.NET Core API
        |
        v
PostgreSQL database
```

The private codebase is organized at a high level as follows:

```text
frontend/     Pages, shared styles, and browser-side behavior
backend/      API controllers, business services, and data models
tests/        Automated API and business-rule tests
docs/         Internal project documentation
```

The frontend presents services and booking flows. The API handles account permissions, scheduling, and booking validation. PostgreSQL stores application records through Entity Framework Core.

## Project status

Active development, with ongoing improvements to support the business and its customers.

## Run the isolated local demo

Install Docker, then run `docker compose up --build` and open http://localhost:5095.

| Demo role | Email | Demo-only password |
| --- | --- | --- |
| Owner | owner@example.test | DemoOwner123! |
| Customer | customer@example.test | DemoCustomer123! |
| Assistant | assistant@example.test | DemoAssistant123! |

All accounts and the sample dog are fictional. Use fictional inputs when exploring the demo.
The demo database uses its own Docker volume and is separate from production. To reset it, run `docker compose down -v` (this deletes only this demo's volume).

### What was changed for the showcase

- Production URLs, contact details, personal names, photographs, and deployment settings were removed from the executable copy.
- Email transports are no-ops, and Google sign-in is disabled. Email templates remain as code examples.
- The API ignores inherited configuration and uses only the fixed local demo database. JWT signing keys are generated at startup, with separate demo issuer, audience, and browser session keys.
- The browser calls only its own API origin. Its content security policy blocks external connections and frames.
- Docker binds published ports to the local computer and keeps the API and database on an internal network without external routing. A local gateway forwards browser requests to the demo API.
- The schema is created from the models; production migrations, data exports, credentials, uploaded media, and private Git history are not included.
- Password reset codes are returned locally for demonstration. Assistant invitation emails are not delivered.

The live-app link above is a documentation link only. No demo code calls the live application. This copy is for local review, not production deployment.

## Tests

Run `dotnet test` with the .NET 10 SDK. Frontend checks are in `tests/frontend/`.
