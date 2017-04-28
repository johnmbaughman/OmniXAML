﻿namespace OmniXaml.ReworkPhases
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Rework;

    public class ObjectBuilder
    {
        private readonly ISmartInstanceCreator instanceCreator;
        private readonly IStringSourceValueConverter converter;
        private readonly IMemberAssigmentApplier assigmentApplier;

        public ObjectBuilder(ISmartInstanceCreator instanceCreator, IStringSourceValueConverter converter, IMemberAssigmentApplier assigmentApplier)
        {
            this.instanceCreator = instanceCreator;
            this.converter = converter;
            this.assigmentApplier = assigmentApplier;
        }

        public InflatedNode Build(ConstructionNode node)
        {
            if (node.SourceValue != null)
            {
                var tryConvert = converter.TryConvert(node.SourceValue, node.ActualInstanceType);
                var isSuccesful = tryConvert.Item1;
                var converted = tryConvert.Item2;

                return new InflatedNode
                {
                    Instance = converted,
                    IsConversionFailed = !isSuccesful,
                    SourceValue = node.SourceValue,
                    InstanceType = node.ActualInstanceType,
                };
            }

            var children = from n in node.Children select Build(n);
            var assignments = (from a in node.Assignments select InflateMemberAssignment(a)).ToList();

            var positionalParameters = from n in node.PositionalParameter select new PositionalParameter(n);
            var creationHints = new CreationHints(new List<NewInjectableMember>(), positionalParameters, new List<object>());

            var instance = instanceCreator.Create(node.ActualInstanceType, creationHints).Instance;

            ApplyAssignments(assignments, instance);

            children.AssociateTo(instance);

            return new InflatedNode
            {
                Instance = instance,
                //UnresolvedAssignments = new HashSet<InflatedMemberAssignment>(unassigned),
            };
        }

        private InflatedMemberAssignment InflateMemberAssignment(MemberAssignment a)
        {
            if (a.SourceValue != null)
            {
                return InflateFromSourceValue(a);
            }
            else
            {
                return InflateFromChildren(a);
            }            
        }

        private InflatedMemberAssignment InflateFromChildren(MemberAssignment a)
        {
            return new InflatedMemberAssignment
            {
                Member = a.Member,
                Children = (from c in a.Children select Build(c)).ToList(),
            };
        }

        private InflatedMemberAssignment InflateFromSourceValue(MemberAssignment a)
        {
            var conversionResult = converter.TryConvert(a.SourceValue, a.Member.MemberType);

            return new InflatedMemberAssignment
            {
                Member = a.Member,
                Children = new List<InflatedNode>
                {
                    new InflatedNode
                    {
                        Instance = conversionResult.Item2,
                        IsConversionFailed = conversionResult.Item1,
                    }
                }
            };
        }

        private void ApplyAssignments(IEnumerable<InflatedMemberAssignment> assignments, object instance)
        {
            foreach (var inflatedMemberAssignment in assignments)
            {
                assigmentApplier.ExecuteAssignment(inflatedMemberAssignment, instance);
            }

        }
    }
}