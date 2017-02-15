﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NWheels.Concurrency;
using NWheels.Entities;
using NWheels.Entities.Core;
using NWheels.Exceptions;
using NWheels.Extensions;
using NWheels.Logging;
using NWheels.Processing.Documents;
using NWheels.Processing.Documents.Core;
using NWheels.UI;
using NWheels.UI.Impl;
using OfficeOpenXml;

namespace NWheels.Stacks.Formats.EPPlus
{
    public class ExcelDataImportOperation
    {
        private readonly FormattedDocument _document;
        private readonly DocumentDesign _design;
        private readonly DocumentDesign.TableElement _tableDesign;
        private readonly int[] _keyColumnIndex;
        private readonly ApplicationEntityService _entityService;
        private readonly IWriteOnlyCollection<DocumentImportIssue> _issues;
        private readonly ApplicationEntityService.EntityHandler _entityHandler;
        private IApplicationDataRepository _domainContext;
        private ApplicationEntityService.EntityCursorRow[] _cursorBuffer;
        private Dictionary<string, ApplicationEntityService.EntityCursorRow> _existingEntityByKey;
        private ApplicationEntityService.EntityCursorMetadata _metaCursor;
        //private int[] _cursorColumnIndex;
        private ExcelPackage _package;
        private ExcelWorksheet _worksheet;

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public ExcelDataImportOperation(
            FormattedDocument document, 
            DocumentDesign design, 
            ApplicationEntityService entityService, 
            IWriteOnlyCollection<DocumentImportIssue> issues)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }
            if (design == null)
            {
                throw new ArgumentNullException("design");
            }
            if (entityService == null)
            {
                throw new ArgumentNullException("entityService");
            }

            _entityService = entityService;
            _issues = issues;
            _document = document;
            _design = design;
            _tableDesign = (design.Contents as DocumentDesign.TableElement);

            if (_tableDesign == null)
            {
                throw new NotSupportedException("Import from excel is only supported for table document design.");
            }

            _entityHandler = _entityService.GetEntityHandler(_tableDesign.BoundEntityName);
            _keyColumnIndex = _tableDesign.Columns.Select((column, index) => column.Binding.IsKey ? index : -1).Where(n => n >= 0).ToArray();
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public void Execute()
        {
            var inputStream = new MemoryStream(_document.Contents);

            using (_package = new ExcelPackage())
            {
                _package.Load(inputStream);

                ExcelImportExportFault? formatValidationFault;
                if (!ValidateFormatSignature(_package, _design, out formatValidationFault))
                {
                    _issues.Add(new DocumentImportIssue(
                        SeverityLevel.Error,
                        formatValidationFault.ToString(),
                        text: string.Format("Document failed to validate (code '{0}'). Expected document format: '{1}'.", formatValidationFault, _design.IdName)));

                    return;
                }

                using (_domainContext = (IApplicationDataRepository)_entityHandler.NewUnitOfWork())
                {
                    RetrieveExistingEntities();
                    BuildExistingEntityIndex();
                    ImportDataRows();
                    
                    _domainContext.CommitChanges();
                }
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void RetrieveExistingEntities()
        {
            var queryOptions = new ApplicationEntityService.QueryOptions(
                _entityHandler.MetaType.QualifiedName, 
                queryParams: new Dictionary<string, string>());

            foreach (var column in _tableDesign.Columns.Where(c => c.Binding.Expression != null))
            {
                queryOptions.SelectPropertyNames.Add(new ApplicationEntityService.QuerySelectItem(column.Binding.Expression)); 
            }

            using (ApplicationEntityService.QueryContext.NewQuery(_entityService, queryOptions))
            {
                var cursor = _entityHandler.QueryCursor(queryOptions);
                _metaCursor = cursor.Metadata;
                _cursorBuffer = cursor.ToArray();
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void BuildExistingEntityIndex()
        {
            var keyBuilder = new StringBuilder();
            _existingEntityByKey = new Dictionary<string, ApplicationEntityService.EntityCursorRow>(capacity: _cursorBuffer.Length);

            for (int i = 0 ; i < _cursorBuffer.Length ; i++)
            {
                var row = _cursorBuffer[i];
                keyBuilder.Clear();

                for (int keyCol = 0; keyCol < _keyColumnIndex.Length; keyCol++)
                {
                    var keyColIndex = _keyColumnIndex[keyCol];
                    var keyColumn = _tableDesign.Columns[keyColIndex];

                    keyBuilder.Append(keyColumn.Binding.ReadValueFromCursor(row, keyColIndex, applyFormat: false).ToStringOrDefault(string.Empty).Trim());
                    keyBuilder.Append('\n');
                }

                _existingEntityByKey[keyBuilder.ToString()] = row;
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void ImportDataRows()
        {
            if (_design.Options.CustomImport != null)
            {
                _design.Options.CustomImport(new CustomImportContext(
                    _design,
                    _package,
                    _worksheet,
                    _entityService,
                    _entityHandler,
                    _domainContext,
                    _metaCursor,
                    _cursorBuffer,
                    _issues
                ));
                return;
            }

            for (int rowNumber = 3 ; !RowIsEmpty(rowNumber) ; rowNumber++)
            {
                ApplicationEntityService.EntityCursorRow existingCursorRow;
                IDomainObject entity;

                if (TryLocateExistingCursorRow(rowNumber, out existingCursorRow))
                {
                    entity = existingCursorRow.Record;
                }
                else
                {
                    entity = _entityHandler.CreateNew();
                }

                PopulateEntityFromWorksheetRow(entity, rowNumber, isNew: existingCursorRow == null);
                _domainContext.GetEntityRepository(entity).Save(entity);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void PopulateEntityFromWorksheetRow(IDomainObject entity, int rowNumber, bool isNew)
        {
            for (int i = 0 ; i < _tableDesign.Columns.Count ; i++)
            {
                var designColumn = _tableDesign.Columns[i];
                var cusrorColumn = _metaCursor.Columns[i];

                if (designColumn.Binding.IsKey)
                {
                    continue;
                }

                PopulateEntityPropertyFromWorksheetCell(entity, cusrorColumn, _worksheet.Cells[rowNumber, i + 1]);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private void PopulateEntityPropertyFromWorksheetCell(IDomainObject entity, ApplicationEntityService.QuerySelectItem cusrorColumn, ExcelRange cell)
        {
            var metaProperty = _metaCursor.EntityMetaType.GetPropertyByName(cusrorColumn.PropertyPath[0]);
            object target = entity;

            for (int nextStep = 1 ; nextStep < cusrorColumn.PropertyPath.Count ; nextStep++)
            {
                if (target == null || !metaProperty.DeclaringContract.ContractType.IsInstanceOfType(target))
                {
                    return;
                }

                target = metaProperty.ReadValue(target);

                if (target == null || metaProperty.Relation == null)
                {
                    break;
                }

                metaProperty = metaProperty.Relation.RelatedPartyType.FindPropertyByNameIncludingDerivedTypes(cusrorColumn.PropertyPath[nextStep]);
            }

            if (target != null && metaProperty.DeclaringContract.ContractType.IsInstanceOfType(target))
            {
                object parsedValue = null;

                if (metaProperty.ClrType.IsInstanceOfType(cell.Value))
                {
                    parsedValue = cell.Value;
                }
                else if (cell.Value is double && metaProperty.ClrType == typeof(DateTime))
                {
                    parsedValue = new DateTime(1900, 1, 1).AddDays((double)cell.Value - 2);
                }
                else if (cell.Value != null)
                {
                    var cellValueString = cell.Value.ToStringOrDefault();
                    parsedValue = metaProperty.ParseStringValue(cellValueString);
                }
                else
                {
                    parsedValue = metaProperty.ClrType.GetDefaultValue();
                }

                metaProperty.WriteValue(target, parsedValue);
            }
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private bool TryLocateExistingCursorRow(int rowNumber, out ApplicationEntityService.EntityCursorRow row)
        {
            var keyBuilder = new StringBuilder();

            for (int keyCol = 0; keyCol < _keyColumnIndex.Length; keyCol++)
            {
                var keyColIndex = _keyColumnIndex[keyCol];
                var cellValue = _worksheet.Cells[rowNumber, keyColIndex + 1].Value.ToStringOrDefault(string.Empty).Trim();
                
                keyBuilder.Append(cellValue);
                keyBuilder.Append('\n');
            }

            return _existingEntityByKey.TryGetValue(keyBuilder.ToString(), out row);
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        //private bool MatchRowsByKey(int sheetRowNumber, ApplicationEntityService.EntityCursorRow cursorRow)
        //{
        //    for (int keyCol = 0 ; keyCol < _keyColumnIndex.Length ; keyCol++)
        //    {
        //        var keyColIndex = _keyColumnIndex[keyCol];
        //        var keyColumn = _tableDesign.Columns[keyColIndex];
        //        var sheetCell = _worksheet.Cells[sheetRowNumber, keyColIndex + 1];
        //        var sheetCellValue = sheetCell.Value;
        //        var cursorKeyValue = keyColumn.Binding.ReadValueFromCursor(cursorRow, keyColIndex, applyFormat: false);

        //        if (cursorKeyValue == null)
        //        {
        //            if (sheetCellValue != null)
        //            {
        //                return false;
        //            }
        //        }
        //        else if (!cursorKeyValue.Equals(sheetCellValue))
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        //private string GetEntityLocatorText(Dictionary<string, string> filter)
        //{
        //    var filterText = string.Join(" & ", filter.Select(kvp => kvp.Key + "=" + kvp.Value));
        //    var locatorText = string.Format("{0}[{1}]", _entityHandler.MetaType.QualifiedName, filterText);
        //    return locatorText;
        //}

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private bool RowIsEmpty(int rowNumber)
        {
            for (int col = 0 ; col < _tableDesign.Columns.Count ; col++)
            {
                if (_worksheet.Cells[rowNumber, col + 1].Value != null)
                {
                    return false;
                }
            }

            return true;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        private bool ValidateFormatSignature(ExcelPackage package, DocumentDesign design, out ExcelImportExportFault? faultSubCode)
        {
            if (package.Workbook.Worksheets.Count != 1 && !(design.Options.PagePerDataRow && design.Options.CustomImport != null))
            {
                faultSubCode = ExcelImportExportFault.WrongNumberOfWorksheets;
                return false;
            }

            foreach (var worksheet in package.Workbook.Worksheets)
            {
                var formatIdName = worksheet.Cells[1, 2].Value as string;

                if (string.IsNullOrEmpty(formatIdName))
                {
                    faultSubCode = ExcelImportExportFault.MissingFormatSignature;
                    return false;
                }

                if (formatIdName != design.IdName)
                {
                    faultSubCode = ExcelImportExportFault.FormatSignatureMismatch;
                    return false;
                }
            }

            _worksheet = package.Workbook.Worksheets.FirstOrDefault();

            faultSubCode = null;
            return true;
        }

        //-----------------------------------------------------------------------------------------------------------------------------------------------------

        public class CustomImportContext
        {
            public CustomImportContext(
                DocumentDesign design, 
                ExcelPackage package, 
                ExcelWorksheet worksheet, 
                ApplicationEntityService entityService, 
                ApplicationEntityService.EntityHandler entityHandler, 
                IApplicationDataRepository domainContext, 
                ApplicationEntityService.EntityCursorMetadata metaCursor, 
                ApplicationEntityService.EntityCursorRow[] cursorBuffer,
                IWriteOnlyCollection<DocumentImportIssue> issues)
            {
                this.Design = design;
                this.Package = package;
                this.Worksheet = worksheet;
                this.EntityService = entityService;
                this.EntityHandler = entityHandler;
                this.DomainContext = domainContext;
                this.MetaCursor = metaCursor;
                this.CursorBuffer = cursorBuffer;
                this.Issues = issues;
            }

            //-------------------------------------------------------------------------------------------------------------------------------------------------

            public DocumentDesign Design { get; private set; }
            public ExcelPackage Package { get; private set; }
            public ExcelWorksheet Worksheet { get; private set; }
            public ApplicationEntityService EntityService { get; private set; }
            public ApplicationEntityService.EntityHandler EntityHandler { get; private set; }
            public IApplicationDataRepository DomainContext { get; private set; }
            public ApplicationEntityService.EntityCursorMetadata MetaCursor { get; private set; }
            public ApplicationEntityService.EntityCursorRow[] CursorBuffer { get; private set; }
            public IWriteOnlyCollection<DocumentImportIssue> Issues { get; private set; }
        }
    }
}