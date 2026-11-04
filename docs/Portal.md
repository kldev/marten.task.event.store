# Halcyon portal: a tour in screenshots

Every screen on this page comes from the e2e run: the real app process on its own PostgreSQL 18 and RustFS
(Testcontainers), with the seed, driven by Playwright in Chromium. Nothing is mocked or retouched.

- Main screens (also shown in the [README](../README.md#screenshots)): `tests/Halcyon.E2E/ScreenshotTests.cs` →
  [`docs/screens/`](screens/)
- The tour below: `tests/Halcyon.E2E/PortalTourTests.cs` → [`docs/screens/portal/`](screens/portal/). Every step
  asserts what the screen should show, so the tour is a test as well as a set of pictures.
- To take them again, run `scripts/screens.sh` (it needs Docker).

The UI is in Polish (*Pula* = pool, *Moje* = mine, *Wszystkie* = all, *Zgłoś* = report). The captions explain each
screen in English.

## Contents

- [Map of the portal](#map-of-the-portal)
- [The requester: public form](#the-requester-public-form)
- [The agent: lists](#the-agent-lists)
- [One ticket's life](#one-tickets-life)
- [Two agents, one click](#two-agents-one-click)
- [Report fields, a handler from Kadry, campaigns](#report-fields-a-handler-from-kadry-campaigns)
- [Reports, exports and charts](#reports-exports-and-charts)
- [On a phone](#on-a-phone)
- [How the screenshots are made](#how-the-screenshots-are-made)

## Map of the portal

```mermaid
flowchart LR
    subgraph public["Without sign-in (PublicLayout)"]
        zglos["/zglos<br/>public report form"]
        login["/logowanie<br/>sign-in · demo agent"]
        nf["/nie-znaleziono"]
    end

    subgraph agent["Signed-in agent (MainLayout: sidebar + alert counters)"]
        pool["/ · /pula<br/>Pool"]
        mine["/moje<br/>My tickets"]
        all["/wszystkie<br/>All tickets"]
        nowe["/nowe<br/>New ticket"]
        ticket["/zgloszenia/{id}<br/>Ticket page"]
        bynum["/zgloszenia/numer/{HAL-…}"]
        agents["/agenci<br/>Agents"]
        people["/osoby<br/>People"]
        subgraph reports["Reports"]
            rindex["/raporty"]
            rcat["/raporty/{kadry-polska · kadry-zagranica ·<br/>finanse · rekrutacja · marketing}"]
            ragents["/raporty/agenci<br/>Agents' work"]
            charts["/wykresy<br/>Charts"]
        end
    end

    subgraph http["Plain HTTP endpoints"]
        auth["POST /auth/login · /auth/login-default · /auth/logout"]
        att["GET /zalaczniki/{ticket}/{file}"]
        exp["GET /raporty/{slug}/eksport.{xlsx|pdf|csv}<br/>GET /raporty/agenci/eksport.{…}"]
        health["/health · /health/ready"]
    end

    zglos -. "Jesteś agentem?" .-> login
    login --> auth --> pool
    pool & mine & all --> ticket
    bynum --> ticket
    nowe --> ticket
    ticket -- "Zamów kampanię" --> nowe
    ticket --> att
    rindex --> rcat & ragents & charts
    rcat & ragents --> exp

    classDef pub fill:#fef3c7,stroke:#d97706,color:#78350f
    classDef ag fill:#ecfbfd,stroke:#0b7a95,color:#132226
    classDef ep fill:#f4f7f8,stroke:#4f6e77,color:#132226
    class zglos,login,nf pub
    class pool,mine,all,nowe,ticket,bynum,agents,people,rindex,rcat,ragents,charts ag
    class auth,att,exp,health ep
```

## The requester: public form

| | |
|---|---|
| <img src="screens/01-public-report.jpg" alt="Public report form" /> | <img src="screens/07-public-report-marketing.jpg" alt="Marketing topics" /> |
| **Public form** `/zglos`: a category tile, a topic (grouped by market for Kadry Zagranica), a description, files, the priority with its SLA, and the requester picked from the list or *incognito*. | **Marketing**: each team has its own topics and its own hint for attachments. Every category also has a *Feedback* topic. |
| <img src="screens/10-public-report-sent.jpg" alt="Report sent" /> | <img src="screens/02-sign-in-demo.jpg" alt="Sign-in" /> |
| **Sent**: the requester gets the ticket number (`HAL-…`). The pool is not visible to them. | **Sign-in** `/logowanie`: e-mail and the team password, or one click as the demo agent. |

```mermaid
journey
    title A requester reports a problem
    section Public form
      Pick a category and a topic: 5: Requester
      Describe it, drop files: 4: Requester
      Choose the priority (SLA shown): 4: Requester
      Pick yourself or go incognito: 3: Requester
      Get the HAL number: 5: Requester
    section Behind the scenes
      Files to S3, then TicketReported + attachments in one save: 5: Halcyon
      The ticket appears in the pool, most urgent on top: 5: Agent
```

## The agent: lists

| | |
|---|---|
| <img src="screens/03-pool.jpg" alt="The pool" /> | <img src="screens/portal/04-my-tickets.jpg" alt="My tickets" /> |
| **Pool** `/`: tickets nobody has taken yet, the most urgent on top. Overdue rows in red, due soon in amber, counters in the top bar. | **My tickets** `/moje`: what the signed-in agent is working on, with *resolve*, *hand over* and *return to pool* on each row. |
| <img src="screens/portal/17-all-tickets-paged.jpg" alt="All tickets, page 2 with the total" /> | <img src="screens/06-all-tickets-hr.jpg" alt="All tickets filtered by HR abroad" /> |
| **All tickets** `/wszystkie`, page 2: the database reads 25 rows plus one to know there is a next page, and the total is a separate `COUNT` behind *Policz wszystkie*. | **Filters**: category, topic, priority, deadline, open or resolved, and a search by title or number. Here: Kadry Zagranica. |
| <img src="screens/08-agents.jpg" alt="Agents" /> | <img src="screens/portal/11-people.jpg" alt="People" /> |
| **Agents** `/agenci`: contacts of the team and how many tickets each one has in progress. | **People** `/osoby`: the requesters (not agents) with department, e-mail and phone; 40 in the seed, 10 of them recruiters. |

## One ticket's life

A printer ticket reported by Anna for Adam Mazur, taken by Anna, raised to *High*, handed over to Michał, commented on
and resolved. Each step adds one event to the ticket's stream, and the timeline on the last screen is that stream.

```mermaid
sequenceDiagram
    autonumber
    actor Anna as Anna Nowak (agent)
    actor Michal as Michał Lewandowski (agent)
    participant P as Ticket page
    participant S as Ticket stream (Marten)
    participant V as TicketView (inline)

    Note over P,S: 01-ticket-in-pool
    Anna->>P: Przejmij
    P->>S: TicketTaken
    S-->>V: status InProgress · assignee Anna
    Note over P,S: 02-priority-preview
    Anna->>P: Medium → High (preview of the new deadline)
    P->>S: TicketPriorityChanged (deadline from the report date)
    Note over P,S: 03-handover-dialog
    Anna->>P: Przekaż → Michał, with a reason
    P->>S: TicketHandedOver (deadline unchanged)
    S-->>V: assignee Michał
    Note over Michal,P: 04-my-tickets
    Michal->>P: comment
    P->>S: TicketCommented
    Michal->>P: Zrealizowane
    P->>S: TicketResolved (in time)
    S-->>V: status Done
    Note over P,V: 05-ticket-done-timeline: six entries, read from the stream
```

| | |
|---|---|
| <img src="screens/portal/01-ticket-in-pool.jpg" alt="A ticket in the pool" /> | <img src="screens/portal/02-priority-preview.jpg" alt="Priority change with a preview" /> |
| **In the pool**: anybody can *Przejmij* (take) it. On the right: category, requester, channel, report date and deadline. | **Priority change**: the new deadline is shown before saving and is always counted from the *report* date, not from now. |
| <img src="screens/portal/03-handover-dialog.jpg" alt="Hand over dialog" /> | <img src="screens/portal/05-ticket-done-timeline.jpg" alt="Done, with the timeline" /> |
| **Hand over**: to another agent, with how many tickets each one has and an optional reason. *Termin realizacji się nie zmienia*: the deadline stays. | **Done**: the timeline holds six entries (reported, taken, priority, handed over, comment, resolved in time), each with who and when. |
| <img src="screens/04-ticket-attachments.jpg" alt="Attachments and comments" /> | <img src="screens/portal/06-ticket-missing.jpg" alt="No such ticket" /> |
| **Attachments**: images are shown inline and other files are downloads, streamed from the private bucket to signed-in agents only. | **By number** `/zgloszenia/numer/HAL-…`: the short number a requester was given; an unknown one says so. |

```mermaid
stateDiagram-v2
    direction TB
    state "In the pool" as InPool
    state "In progress" as InProgress {
        direction LR
        [*] --> Working
        Working --> Working: TicketHandedOver
        Working --> WithHandler: TicketHandlerAssigned<br/>(a person from the department)
        WithHandler --> Working: TicketHandlerAssigned<br/>(nobody)
    }
    state "Done" as Done {
        direction LR
        [*] --> InTime
        [*] --> Late
    }
    [*] --> InPool: TicketReported
    InPool --> InProgress: TicketTaken
    InProgress --> InPool: TicketReturnedToPool
    InProgress --> Done: TicketResolved
    note left of InPool
        Any time: TicketCommented, TicketAttachmentAdded,
        TicketFieldsUpdated. Not when done: TicketPriorityChanged.
    end note
```

## Two agents, one click

| | |
|---|---|
| <img src="screens/portal/07-someone-was-faster.jpg" alt="Someone was faster" /> | Paweł and Krzysztof see the same ticket in the pool and both click *Przejmij*. Paweł's append wins. Krzysztof's `FetchForWriting` expected an older stream version, so Marten refuses the write, and he reads *"Zgłoszenie jest już w realizacji u: Paweł Szymański"*. The ticket leaves his list and nothing is overwritten. |

```mermaid
sequenceDiagram
    autonumber
    actor P as Paweł
    actor K as Krzysztof
    participant W as TicketWriter
    participant DB as PostgreSQL (mt_events)

    P->>W: Take (stream at v1)
    K->>W: Take (stream at v1)
    W->>DB: append TicketTaken, expect v1
    DB-->>W: ok, now v2
    W-->>P: "Przejęto zgłoszenie."
    W->>DB: append TicketTaken, expect v1
    DB-->>W: concurrency error (v2)
    W->>DB: read TicketView
    W-->>K: "Zgłoszenie jest już w realizacji u: Paweł Szymański."
```

## Report fields, a handler from Kadry, campaigns

| | |
|---|---|
| <img src="screens/11-new-employee-fields.jpg" alt="New employee fields" /> | <img src="screens/portal/08-hr-handler.jpg" alt="Handler from Kadry" /> |
| **Report fields**: *Nowy pracownik* in Kadry Polska collects name, department, country (favourites first) and a PESEL checked by its check digit. | **Osoba realizująca**: Anna keeps the payroll ticket but names Halina Nowicka from Kadry to do the work, with a reason. Only Kadry is offered for an HR ticket. |
| <img src="screens/05-new-ticket-picker.jpg" alt="New ticket with the date-time picker" /> | <img src="screens/portal/09-campaign-prefilled.jpg" alt="Campaign order prefilled" /> |
| **Agent's report** `/nowe`: a ticket taken by phone or e-mail, backdated up to 30 days with the custom date-time picker. | **Zamów kampanię**: a recruitment ticket orders a campaign from marketing. The topic, goal, position, headcount, linked ticket and requester are prefilled; the channels (Facebook, OLX Praca) and amount are added. |
| <img src="screens/portal/10-recruitment-with-campaign.jpg" alt="Recruitment ticket with its campaign" /> | **Back on the recruitment ticket**: *Dane do raportu* (client, position, headcount...) and *Kampanie w marketingu*, which lists the ordered campaign with its number, state, channels and amount. |

```mermaid
sequenceDiagram
    autonumber
    actor A as Agent
    participant R as Recruitment ticket
    participant N as /nowe (prefilled)
    participant M as Marketing ticket
    participant V as TicketView

    A->>R: Zamów kampanię w marketingu
    R->>N: marketing.budget · goal "Rekrutacja" · position ·<br/>headcount · related ticket = HAL-… · requester
    A->>N: channels, amount, priority
    N->>M: TicketReported (fields in the event)
    M-->>V: TicketView row with the linked ticket's number
    A->>R: open again
    R->>V: campaigns that name this ticket
    V-->>R: "Kampanie w marketingu" list
```

```mermaid
flowchart LR
    cat{"ticket's category"} -- "Kadry Polska · Kadry Zagranica · Legalizacja" --> kadry["people from Kadry"]
    cat -- "Rekrutacja" --> rek["people from Rekrutacja"]
    cat -- "Finanse" --> fin["people from Finanse"]
    cat -- "IT · Prawny · Marketing" --> any["anybody from the list"]
    kadry & rek & fin & any --> ev["TicketHandlerAssigned<br/>(+ reason)"]
    ev --> stays["the ticket stays with the agent;<br/>the handler is shown on the ticket, the lists and the timeline"]
```

## Reports, exports and charts

| | |
|---|---|
| <img src="screens/portal/12-reports-index.jpg" alt="Reports" /> | <img src="screens/portal/13-report-hr-poland.jpg" alt="Kadry Polska report" /> |
| **Reports** `/raporty`: one per team with report fields, the agents' work and the charts. | **Kadry Polska**: one column per report field, with filters (dates, topic, open or resolved) and XLSX, PDF and CSV exports. |
| <img src="screens/12-report-hr-abroad.jpg" alt="Kadry Zagranica report" /> | <img src="screens/portal/14-report-finance.jpg" alt="Finance report" /> |
| **Kadry Zagranica**: the market (Belgia, Niemcy, Francja) as a filter. | **Finanse**: invoice numbers, contractors, amounts and currencies, due dates, cost centres. |
| <img src="screens/portal/15-report-recruitment.jpg" alt="Recruitment report" /> | <img src="screens/13-report-agents.jpg" alt="Agents' work" /> |
| **Rekrutacja**: client, position, headcount, place and dates of work. | **Agents' work**: taken, resolved and late per agent, by calendar week, month and 3 months. |
| <img src="screens/14-charts.jpg" alt="Charts" /> | <img src="screens/portal/16-charts-hr-abroad.jpg" alt="Charts for Kadry Zagranica" /> |
| **Charts** `/wykresy`: key figures, flow in and out, backlog, deadlines kept, SLA use per priority, the queue now, categories, agents, and a weekday × hour heatmap. | **The same, for one category**: here Kadry Zagranica. Everything is drawn on the server as SVG and HTML, hover values work with CSS alone, and each chart has a table of the same numbers under it. |

```mermaid
flowchart LR
    ev[("mt_events<br/>ticket streams")] -- "inline projection,<br/>same transaction" --> tv[("TicketView<br/>status · assignee · deadline ·<br/>category · fields · handler")]
    tv --> cr["CategoryReports<br/>one column per report field"]
    tv --> ar["AgentReport<br/>calendar week / month / 3 months"]
    tv --> cq["ChartQueries<br/>one query → ChartModel"]
    cr & ar --> page["report page<br/>filters · table"]
    cr & ar --> exp["ReportExports"]
    exp --> xlsx["XLSX<br/>ClosedXML"]
    exp --> pdf["PDF<br/>QuestPDF"]
    exp --> csv["CSV<br/>; and UTF-8 BOM for a Polish Excel"]
    cq --> svg["SVG + HTML charts<br/>+ a table under each"]
```

## On a phone

| | |
|---|---|
| <img src="screens/portal/18-public-form-phone.jpg" width="60%" alt="Public form on a phone" /> | <img src="screens/09-pool-phone.jpg" width="60%" alt="Pool on a phone" /> |
| **Public form** at 390 px: the tiles are stacked, Legalizacja is picked and its topics are shown. | **Pool** on a phone: time left is shown under the title. |
| <img src="screens/portal/19-ticket-phone.jpg" width="60%" alt="Ticket on a phone" /> | **A critical ticket** with under an hour left: *due soon* in amber. The side panel moves under the content. |

## How the screenshots are made

```mermaid
flowchart TB
    script["scripts/screens.sh<br/>HALCYON_SCREENS_TO_DOCS=1"] --> test["dotnet test Halcyon.E2E<br/>ScreenshotTests + PortalTourTests"]
    subgraph fixture["E2EEnvironment (one per run)"]
        direction LR
        pg[("postgres:18<br/>Testcontainers")]
        s3[("rustfs:1.0.0<br/>Testcontainers")]
        mig["DbMigrator seed<br/>10 agents · 40 people · 238 tickets"]
        app["Halcyon.App process<br/>schema changes off"]
        chrome["Chromium<br/>Playwright"]
        pg --> mig --> app
        s3 --> app
    end
    test --> fixture
    chrome -- "pl-PL · Europe/Warsaw" --> app
    test --> shot["Screens.ShotAsync<br/>wait for network idle,<br/>window as tall as the page, JPEG 85"]
    shot --> out["bin/…/screens/**"]
    shot --> docs["docs/screens/*.jpg (README)<br/>docs/screens/portal/*.jpg (this page)"]
```

Ticket numbers, the suffixes in titles (for example `5af8b4`) and the times differ on every run. The tests make each
title unique so that they can find their own rows among the 200+ tickets from the seed.
