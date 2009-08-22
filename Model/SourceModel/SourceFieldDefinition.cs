﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace WXML.Model.Descriptors
{
    public class SourceFieldDefinition
    {
        private SourceFragmentDefinition _tbl;
        protected internal string _column;

        private bool _isNotNullable;
        private string _type;
        private bool _identity;
        private int? _sz;
        protected internal string _defaultValue;

        protected internal SourceFieldDefinition()
        {
        }

        public SourceFieldDefinition(SourceFragmentDefinition sf, string column)
            :this(sf, column, null, true, null, false, null)
        {
            
        }

        public SourceFieldDefinition(SourceFragmentDefinition sf, string column, int? sourceTypeSize,
            bool isNullable, string type, bool identity, string defaultValue)
        {
            _tbl = sf;
            _column = column;
            _sz = sourceTypeSize;
            _isNotNullable = !isNullable;
            _type = type;
            _identity = identity;
            _defaultValue = defaultValue;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SourceFieldDefinition);
        }

        public bool Equals(SourceFieldDefinition obj)
        {
            if (obj == null)
                return false;

            return ToString() == obj.ToString();
        }

        public override string ToString()
        {
            return _tbl.Selector + _tbl.Name + _column;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public SourceFragmentDefinition SourceFragment
        {
            get { return _tbl; }
            set { _tbl = value; }
        }

        public string SourceFieldExpression
        {
            get { return _column; }
            set { _column = value; }
        }

        public bool IsAutoIncrement
        {
            get { return _identity; }
            set { _identity = value; }
        }

        public bool IsNullable
        {
            get { return !_isNotNullable; }
            set { _isNotNullable = !value; }
        }

        public int? SourceTypeSize
        {
            get { return _sz; }
            set { _sz = value; }
        }

        public string SourceType
        {
            get { return _type; }
            set { _type = value; }
        }

        public string DefaultValue
        {
            get { return _defaultValue; }
            set { _defaultValue = value; }
        }

        public bool IsPK
        {
            get
            {
                return Constraints.Any(c => c.ConstraintType == SourceConstraint.PrimaryKeyConstraintTypeName);
            }
        }

        public bool IsFK
        {
            get
            {
                return Constraints.Any(c => c.ConstraintType == SourceConstraint.ForeignKeyConstraintTypeName);
            }
        }

        public IEnumerable<SourceConstraint> Constraints
        {
            get
            {
                return SourceFragment.Constraints.Where(c =>
                    c.SourceFields.Contains(this)
                );
            }
        }

        public Field2DbRelations GetAttributes()
        {
            Field2DbRelations attrs = Field2DbRelations.None;
            if (IsPK)
            {
                if (!IsAutoIncrement)
                    attrs = Field2DbRelations.PK;
                else
                    attrs = Field2DbRelations.PrimaryKey;
            }
            else
            {
                if (!IsNullable && !string.IsNullOrEmpty(DefaultValue))
                    attrs = Field2DbRelations.InsertDefault | Field2DbRelations.SyncInsert;
                else if (!string.IsNullOrEmpty(DefaultValue))
                    attrs = Field2DbRelations.SyncInsert;
            }

            return attrs;
        }
    }
}
