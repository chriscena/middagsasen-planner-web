CREATE TABLE [dbo].[EventTemplateCompetencyRequirements]
(
  EventTemplateCompetencyRequirementId int not null identity,
  constraint PK_EventTemplateCompetencyRequirements PRIMARY KEY (EventTemplateCompetencyRequirementId),
  EventTemplateId int not null,
  constraint FK_EventTemplateCompetencyRequirements_EventTemplates foreign key (EventTemplateId) references EventTemplates(EventTemplateId) on delete cascade,
  CompetencyId int not null,
  constraint FK_EventTemplateCompetencyRequirements_Competencies foreign key (CompetencyId) references Competencies(CompetencyId) on delete cascade,
  MinimumRequired int not null,
  constraint UQ_EventTemplateCompetencyRequirements_EventTemplateId_CompetencyId unique(EventTemplateId, CompetencyId),
)
