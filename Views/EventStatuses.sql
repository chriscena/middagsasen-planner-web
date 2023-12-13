create view EventStatuses as
  with 
    Users_CTE(EventResourceId, Users) as 
      (select EventResourceId, count(EventResourceUserId) as Users from EventResourceUsers group by EventResourceId),
    Resources_CTE(EventId, EventResourceId, MinimumStaff, Users) as
      (select EventId, r.EventResourceId, MinimumStaff, coalesce(Users, 0) as Users from EventResources r left outer join Users_CTE as u on r.EventResourceId = u.EventResourceId)

    select e.EventId, StartTime, MinimumStaff, Users, case when (MinimumStaff > Users) then 1 else 0 end as MissingStaff
    from Events e
    join Resources_CTE
      ru on ru.EventId = e.EventId