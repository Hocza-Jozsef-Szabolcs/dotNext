using System;
using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents synchronized block of code.
    /// </summary>
    /// <see href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</see>
    public sealed class LockExpression: Expression
    {
        /// <summary>
        /// Represents constructor of synchronized block of code.
        /// </summary>
        /// <param name="syncRoot">The variable representing monitor object.</param>
        /// <returns>The body of synchronized block of code.</returns>
        public delegate Expression Statement(ParameterExpression syncRoot);

        private readonly BinaryExpression assignment;
        private Expression body;

        internal LockExpression(Expression syncRoot)
        {
            if (syncRoot is ParameterExpression syncVar)
                SyncRoot = syncVar;
            else
            {
                SyncRoot = Variable(typeof(object), "syncRoot");
                assignment = Assign(SyncRoot, syncRoot);
            }
        }

        /// <summary>
        /// Creates a new synchronized block of code.
        /// </summary>
        /// <param name="syncRoot">The monitor object.</param>
        /// <param name="body">The delegate used to construct synchronized block of code.</param>
        /// <returns>The synchronized block of code.</returns>
        public static LockExpression Create(Expression syncRoot, Statement body)
        {
            var result = new LockExpression(syncRoot);
            result.Body = body(result.SyncRoot);
            return result;
        }

        /// <summary>
        /// Creates a new synchronized block of code.
        /// </summary>
        /// <param name="syncRoot">The monitor object.</param>
        /// <param name="body">The body of the code block.</param>
        /// <returns>The synchronized block of code.</returns>
        public static LockExpression Create(Expression syncRoot, Expression body)
            => new LockExpression(syncRoot) { Body = body };

        /// <summary>
        /// Represents monitor object.
        /// </summary>
        public ParameterExpression SyncRoot { get;  }

        /// <summary>
        /// Gets body of the synchronized block of code.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        /// <summary>
        /// Always returns <see langword="true"/> because
        /// this expression is <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Gets type of this expression.
        /// </summary>
        public override Type Type => Body.Type;

        /// <summary>
        /// Produces actual code for the synchronization block.
        /// </summary>
        /// <returns>The actual code for the synchronization block.</returns>
        public override Expression Reduce()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = TryFinally(Body, Call(monitorExit, SyncRoot));
            return assignment is null ?
                    Block(Call(monitorEnter, SyncRoot), body) :
                    Block(Sequence.Singleton(SyncRoot), assignment, Call(monitorEnter, SyncRoot), body);
        }
    }
}