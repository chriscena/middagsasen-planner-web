CREATE TABLE WorkHours
(
  WorkHourId INT not null identity, 
  constraint PK_WorkHours PRIMARY key(WorkHourId),
  UserId INT not null,
  constraint FK_WorkHours_Users_UserId foreign key (UserId) REFERENCES Users(UserId),
  -- Ubrukt: timer registreres uten kobling til vakt. Kolonnen kan fjernes senere.
  ShiftId INT null,
  StartTime DATETIME not null, 
  EndTime DATETIME null,
  Description NVARCHAR(max) null,
  ApprovedBy INT null,
  constraint FK_WorkHours_Users_ApprovedBy foreign key (ApprovedBy) REFERENCES Users(UserId),
  ApprovalStatus INT null,
  ApprovedTime DATETIME null,
  ModifiedBy INT null,
  constraint FK_WorkHours_Users_ModifiedBy foreign key (ModifiedBy) REFERENCES Users(UserId),
  ModifiedTime DATETIME null,
  -- Hvem som førte timene (ikke nødvendigvis eieren UserId) og når. Settes av API-et ved opprettelse.
  -- Nullable fordi API-versjonen før disse kolonnene ikke setter dem; Script.PostDeployment.sql fyller
  -- inn manglende verdier (CreatedBy = UserId, CreatedTime = StartTime) ved hver deploy.
  CreatedBy INT null,
  constraint FK_WorkHours_Users_CreatedBy foreign key (CreatedBy) REFERENCES Users(UserId),
  CreatedTime DATETIME null,
)
