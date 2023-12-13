create view HallOfFame as
  select top 50 u.UserId, u.FirstName, u.LastName, count(eru.EventResourceUserId) Shifts  from Users u
  join EventResourceUsers eru on eru.UserId = u.UserId
  Where eru.EndTime < cast(cast(GETDATE() as date) as datetime)
  GROUP by u.UserId, u.FirstName, u.LastName
  order by Shifts desc, u.FirstName