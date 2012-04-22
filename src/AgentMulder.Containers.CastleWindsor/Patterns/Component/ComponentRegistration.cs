﻿using System;
using System.Collections.Generic;
using AgentMulder.ReSharper.Domain.Registrations;
using AgentMulder.ReSharper.Domain.Search;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Services.CSharp.StructuralSearch;
using JetBrains.ReSharper.Psi.Services.CSharp.StructuralSearch.Placeholders;
using JetBrains.ReSharper.Psi.Services.StructuralSearch;
using JetBrains.ReSharper.Psi.Tree;

namespace AgentMulder.Containers.CastleWindsor.Patterns.Component
{
    internal sealed class ComponentRegistration : RegistrationBase
    {
        private static readonly ComponentForRegistrationPattern componentForPattern = new ComponentForRegistrationPattern();
        private readonly IRegistration implementedByPattern;

        public ComponentRegistration()
            :base(componentForPattern.Pattern)
        {
            implementedByPattern = new ComponentImplementedByRegistrationPattern();
        }

        public override IComponentRegistrationCreator CreateComponentRegistrationCreator()
        {
            IComponentRegistrationCreator componentForCreator = componentForPattern.CreateComponentRegistrationCreator();

            return new CompositeCreator(componentForCreator, implementedByPattern);
        }

        private sealed class ComponentForRegistrationPattern : IRegistration
        {
            private static readonly IStructuralSearchPattern pattern =
                new CSharpStructuralSearchPattern("$component$.For<$service$>()",
                                                  new ExpressionPlaceholder("component", "Castle.MicroKernel.Registration.Component"),
                                                  new TypePlaceholder("service"));

            public IStructuralSearchPattern Pattern
            {
                get { return pattern; }
            }

            public IStructuralMatcher CreateMatcher()
            {
                return pattern.CreateMatcher();
            }

            public IComponentRegistrationCreator CreateComponentRegistrationCreator()
            {
                return new ServiceRegistrationCreator();
            }

            private sealed class ServiceRegistrationCreator : IComponentRegistrationCreator
            {
                public IEnumerable<IComponentRegistration> CreateRegistrations(params IStructuralMatchResult[] matchResults)
                {
                    foreach (IStructuralMatchResult match in matchResults)
                    {
                        var matchedType = match.GetMatchedType("service") as IDeclaredType;
                        if (matchedType != null)
                        {
                            ITypeElement typeElement = matchedType.GetTypeElement(match.MatchedElement.GetPsiModule());
                            yield return new ReSharper.Domain.Registrations.ComponentRegistration(match.GetDocumentRange(), typeElement);
                        }
                    }
                }
            }
        }

        private sealed class ComponentImplementedByRegistrationPattern : IRegistration
        {
            private static readonly IStructuralSearchPattern pattern =
                new CSharpStructuralSearchPattern("$component$.For<$service$>().ImplementedBy<$impl$>()",
                                                  new ExpressionPlaceholder("component", "Castle.MicroKernel.Registration.Component"),
                                                  new TypePlaceholder("service"),
                                                  new TypePlaceholder("impl"));

            public IStructuralMatcher CreateMatcher()
            {
                return pattern.CreateMatcher();
            }

            public IComponentRegistrationCreator CreateComponentRegistrationCreator()
            {
                return new ServiceWithImplementationCreator();
            }

            private sealed class ServiceWithImplementationCreator : IComponentRegistrationCreator
            {
                public IEnumerable<IComponentRegistration> CreateRegistrations(params IStructuralMatchResult[] matchResults)
                {
                    foreach (IStructuralMatchResult match in matchResults)
                    {
                        var matchedType = match.GetMatchedType("impl") as IDeclaredType;
                        if (matchedType != null)
                        {
                            ITypeElement typeElement = matchedType.GetTypeElement(match.MatchedElement.GetPsiModule());
                            yield return new ReSharper.Domain.Registrations.ComponentRegistration(match.GetDocumentRange(), typeElement);
                        }
                    }
                }
            }

        }

        private sealed class CompositeCreator : IComponentRegistrationCreator
        {
            private readonly IComponentRegistrationCreator baseCreator;
            private readonly IRegistration otherPattern;

            public CompositeCreator(IComponentRegistrationCreator baseCreator, IRegistration otherPattern)
            {
                this.baseCreator = baseCreator;
                this.otherPattern = otherPattern;
            }

            public IEnumerable<IComponentRegistration> CreateRegistrations(params IStructuralMatchResult[] matchResults)
            {
                IStructuralMatcher otherMatcher = otherPattern.CreateMatcher();

                var results = new List<IComponentRegistration>();

                foreach (IStructuralMatchResult match in matchResults)
                {
                    // todo hack fix this 
                    IInvocationExpression parentExpression = FindParentExpression(match.MatchedElement);
                    if (parentExpression != null)
                    {
                        IStructuralMatchResult otherMatchResult = otherMatcher.Match(parentExpression);
                        if (otherMatchResult.Matched)
                        {
                            IComponentRegistrationCreator creator = otherPattern.CreateComponentRegistrationCreator();
                            results.AddRange(creator.CreateRegistrations(otherMatchResult));
                            continue;
                        }
                    }
                    
                    results.AddRange(baseCreator.CreateRegistrations(match));
                }

                return results;
            }

            private IInvocationExpression FindParentExpression(ITreeNode matchedElement)
            {
                for (ITreeNode parent = matchedElement.Parent; parent != null; parent = parent.Parent)
                {
                    var invocationExpression = parent as IInvocationExpression;
                    if (invocationExpression != null)
                    {
                        return invocationExpression;
                    }
                }

                return null;
            }
        }
    }
}