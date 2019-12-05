﻿using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class TaskResultBinder : CallSiteBinder
    {
        private const string PropertyName = nameof(Task<int>.Result);
        private const BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

        private static MemberExpression BindMissingProperty(Expression target, out Expression restrictions)
        {
            restrictions = Expression.TypeEqual(target, typeof(Task));
            return Expression.Field(null, typeof(Missing), nameof(Missing.Value));
        }

        private static Expression BindProperty(PropertyInfo resultProperty, Expression target, out Expression restrictions)
        {
            restrictions = Expression.TypeIs(target, resultProperty.DeclaringType);
            //reinterpret reference type without casting because it is protected by restriction
            target = Expression.Call(typeof(Unsafe), nameof(Unsafe.As), new[] { resultProperty.DeclaringType }, target);
            target = Expression.Property(target, resultProperty);
            return target.Type.IsValueType ? Expression.Convert(target, typeof(object)) : target;
        }

        private static Expression Bind(object targetValue, Expression target, LabelTarget returnLabel)
        {
            PropertyInfo? property = targetValue.GetType().GetProperty(PropertyName, PropertyFlags);
            target = property is null ?
                BindMissingProperty(target, out var restrictions) :
                BindProperty(property, target, out restrictions);

            target = Expression.Return(returnLabel, target);
            target = Expression.Condition(restrictions, target, Expression.Goto(UpdateLabel));
            return target;
        }

        public override Expression Bind(object[] args, ReadOnlyCollection<ParameterExpression> parameters, LabelTarget returnLabel) => Bind(args[0], parameters[0], returnLabel);

        public override T BindDelegate<T>(CallSite<T> site, object[] args)
        {
            return base.BindDelegate(site, args);
        }
    }
}
