# Princess Dog Walker

A web application for a dog walking and pet-care business, built to help customers explore services, check availability, and request care while giving the business owner a central place to manage bookings.

**[Visit the web app](https://princessdogwalker.onrender.com/assistant.html)**

## About the project

I created Princess Dog Walker for my friend to support her growing pet-care business. The project brings service information, scheduling, customer accounts, and day-to-day booking management into one application.

This public repository is a project overview. The application source code and development history are maintained separately in a private repository.

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
