# Detailed Architecture (existing)

```mermaid
flowchart LR
  subgraph VC["Valuator cluster"]
      direction TB
          V2["<b>Valuator #2</b><br>ASP.NET Core Razor Pages<br>Второй экземпляр веб-приложения"]
          V1["<b>Valuator #1</b><br>ASP.NET Core Razor Pages<br>Сохраняет TEXT-{id} и SIMILARITY-{id}<br>в Redis и публикует id в RabbitMQ"]
  end
  subgraph RCW["RankCalculator workers"]
      direction TB
          RC2["<b>RankCalculator #2</b><br>.NET BackgroundService<br>Второй competing consumer"]
          RC1["<b>RankCalculator #1</b><br>.NET BackgroundService<br>Читает Id из RabbitMQ, берет TEXT-{id} из Redis,<br>вычисляет rank, сохраняет RANK-{id}<br>Публикует событие RankCalculated"]
  end
  subgraph ELW["EventsLogger workers"]
      direction TB
          EL2["<b>EventsLogger #2</b><br>.NET Console App<br>Второй экземпляр логгера событий"]
          EL1["<b>EventsLogger #1</b><br>.NET Console App<br>Подписывается на RankCalculated<br>и SimilarityCalculated события"]
  end
  subgraph System["PA5 Valuator System"]
      direction LR
          Nginx["<b>nginx-lb</b><br>Nginx<br>Балансирует HTTP и WebSocket-запросы"]
          VC
          MQ[("<b>RabbitMQ</b><br>Message Broker<br>Очередь/обменник:<br>valuator.processing.rank")]
          RCW
          Redis[("<b>Redis</b><br>Key-Value Store<br>Хранит:<br>TEXT-{id}<br>SIMILARITY-{id}<br>RANK-{id}<br>ALL_TEXTS")]
          EventsMQ[("<b>RabbitMQ Events</b><br>Fanout Exchanges:<br>events.rank<br>events.similarity")]
          ELW
  end
      User(("Пользователь")) -- HTTP открывает сайт,<br>отправляет форму,<br>смотрит summary --> Nginx
      User -. WebSocket подписка на обновления .-> Nginx
      Nginx -- HTTP/WebSocket --> V1 & V2
      V1 -- публикует id задания --> MQ
      V2 -- публикует id задания --> MQ
      MQ -- id задания --> RC1 & RC2
      V1 -. "сохраняет TEXT-{id}<br>читает RANK-{id}" .-> Redis
      V2 -. "сохраняет TEXT-{id}<br>читает RANK-{id}" .-> Redis
      RC1 == "читает TEXT-{id}<br>записывает RANK-{id}" ===> Redis
      RC2 == "читает TEXT-{id}<br>записывает RANK-{id}" ===> Redis
      RC1 -- "RankCalculated event" --> EventsMQ
      V1 -- "SimilarityCalculated event" --> EventsMQ
      EventsMQ --> EL1 & EL2
      EventsMQ --> V1
      V1 -- "SignalR rankUpdated" --> User
```

# C4 Container Diagram (LW5)

```mermaid
C4Container
title PA5 Distributed Programming - Container Diagram

Person(user, "Пользователь", "Отправляет текст и смотрит результат анализа")

System_Boundary(system, "Valuator System") {
    Container(nginx, "Nginx", "Reverse Proxy / Load Balancer", "Маршрутизирует HTTP/WebSocket запросы к экземплярам Valuator")
    Container(valuator, "Valuator", "ASP.NET Core Razor Pages + SignalR", "Принимает текст, сохраняет исходные данные, публикует сообщения, показывает Summary и отправляет realtime-обновления")
    Container(rankCalculator, "RankCalculator", ".NET BackgroundService", "Читает задания, ждёт 3-15 сек, вычисляет rank и публикует событие RankCalculated")
    Container(eventsLogger, "EventsLogger", ".NET Console App", "Подписывается на события и пишет их в лог")
    ContainerDb(redis, "Redis", "Key-Value Store", "TEXT-{id}, SIMILARITY-{id}, RANK-{id}, ALL_TEXTS")
    ContainerQueue(rabbitmq, "RabbitMQ", "Message Broker", "Очередь задач rank + fanout exchanges events.rank/events.similarity")
}

Rel(user, nginx, "Открывает сайт и отправляет форму", "HTTP")
Rel(nginx, valuator, "Проксирует запросы", "HTTP/WebSocket")
Rel(valuator, redis, "Сохраняет/читает данные", "Redis protocol")
Rel(valuator, rabbitmq, "Публикует rank task и SimilarityCalculated", "AMQP")
Rel(rankCalculator, rabbitmq, "Читает rank task, публикует RankCalculated", "AMQP")
Rel(rankCalculator, redis, "Читает TEXT-{id}, пишет RANK-{id}", "Redis protocol")
Rel(eventsLogger, rabbitmq, "Подписывается на события", "AMQP")
Rel_Back(valuator, user, "Отправляет обновление rank в Summary", "SignalR / WebSocket")
```

## Примечания

- `Valuator` и `RankCalculator` могут запускаться в нескольких экземплярах.
- Для `Summary` используется подписка клиента в SignalR-группу по `id`.
- `RankNotificationService` в `Valuator` получает `RankCalculated` из RabbitMQ и пушит `rankUpdated` в браузер.