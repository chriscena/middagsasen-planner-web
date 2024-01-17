create view HallOfFame as
  select u.UserId, u.FirstName, u.LastName, count(distinct er.EventId) Shifts  from Users u
  join EventResourceUsers eru on eru.UserId = u.UserId
  join EventResources er on er.EventResourceId = eru.EventResourceId
  Where eru.EndTime < cast(cast(GETDATE() as date) as datetime)
  GROUP by u.UserId, u.FirstName, u.LastName
  order by Shifts desc, u.FirstName