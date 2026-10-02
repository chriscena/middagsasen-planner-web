create view HallOfFame as
  select u.UserId, u.FirstName, u.LastName, count(distinct er.EventId) Shifts  from Users u
  join EventResourceUsers eru on eru.UserId = u.UserId
  join EventResources er on er.EventResourceId = eru.EventResourceId
  -- EndTime lagres som norsk lokal tid uten tidssone, mens serveren går i UTC, så «i dag» må være norsk dato.
  Where eru.EndTime < cast(cast(SYSDATETIMEOFFSET() AT TIME ZONE 'W. Europe Standard Time' as date) as datetime)
  GROUP by u.UserId, u.FirstName, u.LastName