using System.Globalization;
using Xping.Sdk.Core.Common;

namespace Xping.Sdk.Core.Session.Comparison.Comparers;

/// <summary>
/// Implements comparison logic for <see cref="TestSession.Steps"/> property.
/// </summary>
public class TestStepsComparer : ITestSessionComparer
{
    /// <summary>
    /// Compares <see cref="TestSession.Steps"/> property of two <see cref="TestSession"/> instances and identifies 
    /// differences.
    /// </summary>
    /// <param name="session1">The first TestSession instance.</param>
    /// <param name="session2">The second TestSession instance.</param>
    /// <returns>A DiffResult object containing the differences.</returns>
    public DiffResult Compare(TestSession session1, TestSession session2)
    {
        if (ReferenceEquals(session1, session2))
        {
            return DiffResult.Empty;
        }

        if (session1 == null || session2 == null)
        {
            return DiffResult.Empty;
        }

        var result = new DiffResult();

        foreach (TestStep testStep1 in session1.Steps)
        {
            bool foundInSession2 = false;

            foreach (TestStep testStep2 in session2.Steps)
            {
                if (testStep1.Name == testStep2.Name &&
                    testStep1.TestComponentIteration == testStep2.TestComponentIteration)
                {
                    foundInSession2 = true;

                    CompareType(result, testStep1, testStep2);
                    CompareExecutionDuration(result, testStep1, testStep2);
                    CompareResultType(result, testStep1, testStep2);
                    CompareErrorMessage(result, testStep1, testStep2);
                    ComparePropertyBag(result, testStep1, testStep2);
                }
            }

            if (!foundInSession2)
            {
                var name = GetPropertyName(
                    testStep1.Name,
                    nameof(TestStep.TestComponentIteration),
                    testStep1.TestComponentIteration.ToString(CultureInfo.InvariantCulture));
                result.AddDifference(new Difference(
                    PropertyName: name,
                    Value1: testStep1,
                    Value2: null!,
                    Type: DifferenceType.Removed));
            }
        }

        foreach (TestStep testStep2 in session2.Steps)
        {
            bool foundInSession1 = false;

            foreach (TestStep testStep1 in session1.Steps)
            {
                if (testStep2.Name == testStep1.Name &&
                    testStep2.TestComponentIteration == testStep1.TestComponentIteration)
                {
                    foundInSession1 = true;
                }
            }

            if (!foundInSession1)
            {
                var name = GetPropertyName(
                    testStep2.Name,
                    nameof(TestStep.TestComponentIteration),
                    testStep2.TestComponentIteration.ToString(CultureInfo.InvariantCulture));
                result.AddDifference(
                    new Difference(
                        PropertyName: name,
                        Value1: null!,
                        Value2: testStep2,
                        Type: DifferenceType.Added));
            }
        }

        return result;
    }

    // PropertyName string format of the TestStep property representing PropertyName in Difference object.
    // Example: TestStep("Dns lookup").PropertyBag["HttpStatus"]
    private static string GetPropertyName(string stepName, string propertyName, string? propertyBagKeyName = null)
    {
        string name = $"{nameof(TestStep)}(\"{stepName}\").{propertyName}";

        if (!string.IsNullOrEmpty(propertyBagKeyName))
        {
            return name + $"[\"{propertyBagKeyName}\"]";
        }

        return name;
    }

    private static void CompareType(DiffResult result, TestStep testStep1, TestStep testStep2)
    {
        // Comparison is performed only if test steps match by name and iteration count of the test component.
        // Retrieving the test step name from either is acceptable due to their equivalence.
        string stepName = testStep1.Name;

        if (testStep1.Type != testStep2.Type)
        {
            result.AddDifference(new(
                PropertyName: GetPropertyName(stepName, nameof(TestStep.Type)),
                Value1: testStep1.Type,
                Value2: testStep2.Type,
                Type: DifferenceType.Changed));
        }
    }

    private static void CompareExecutionDuration(DiffResult result, TestStep testStep1, TestStep testStep2)
    {
        // Comparison is performed only if test steps match by name and iteration count of the test component.
        // Retrieving the test step name from either is acceptable due to their equivalence.
        string stepName = testStep1.Name;

        if (testStep1.Duration != testStep2.Duration)
        {
            result.AddDifference(new(
                PropertyName: GetPropertyName(stepName, nameof(TestStep.Duration)),
                Value1: testStep1.Duration,
                Value2: testStep2.Duration,
                Type: DifferenceType.Changed));
        }
    }

    private static void CompareResultType(DiffResult result, TestStep testStep1, TestStep testStep2)
    {
        // Comparison is performed only if test steps match by name and iteration count of the test component.
        // Retrieving the test step name from either is acceptable due to their equivalence.
        string stepName = testStep1.Name;

        if (testStep1.Result != testStep2.Result)
        {
            result.AddDifference(new(
                PropertyName: GetPropertyName(stepName, nameof(TestStep.Result)),
                Value1: testStep1.Result,
                Value2: testStep2.Result,
                Type: DifferenceType.Changed));
        }
    }

    private static void CompareErrorMessage(DiffResult result, TestStep testStep1, TestStep testStep2)
    {
        // Comparison is performed only if test steps match by name and iteration count of the test component.
        // Retrieving the test step name from either is acceptable due to their equivalence.
        string stepName = testStep1.Name;

        if (testStep1.ErrorMessage != testStep2.ErrorMessage)
        {
            if (!string.IsNullOrEmpty(testStep1.ErrorMessage) && string.IsNullOrEmpty(testStep2.ErrorMessage))
            {
                result.AddDifference(new(
                    PropertyName: GetPropertyName(stepName, nameof(TestStep.ErrorMessage)),
                    Value1: testStep1.ErrorMessage,
                    Value2: null,
                    Type: DifferenceType.Removed));
            }
            else if (string.IsNullOrEmpty(testStep1.ErrorMessage) && !string.IsNullOrEmpty(testStep2.ErrorMessage))
            {
                result.AddDifference(new(
                    PropertyName: GetPropertyName(stepName, nameof(TestStep.ErrorMessage)),
                    Value1: null,
                    Value2: testStep2.ErrorMessage,
                    Type: DifferenceType.Added));
            }
            else if (!string.IsNullOrEmpty(testStep1.ErrorMessage) && !string.IsNullOrEmpty(testStep2.ErrorMessage))
            {
                result.AddDifference(new(
                    PropertyName: GetPropertyName(stepName, nameof(TestStep.ErrorMessage)),
                    Value1: testStep1.ErrorMessage,
                    Value2: testStep2.ErrorMessage,
                    Type: DifferenceType.Changed));
            }
        }
    }

    private static void ComparePropertyBag(DiffResult result, TestStep testStep1, TestStep testStep2)
    {
        // Comparison is performed only if test steps match by name and iteration count of the test component.
        // Retrieving the test step name from either is acceptable due to their equivalence.
        string stepName = testStep1.Name;

        if (testStep1.PropertyBag != null && testStep2.PropertyBag == null)
        {
            result.AddDifference(new(
                PropertyName: GetPropertyName(stepName, nameof(TestStep.PropertyBag)),
                Value1: testStep1.PropertyBag,
                Value2: null,
                Type: DifferenceType.Removed));
        }
        else if (testStep1.PropertyBag == null && testStep2.PropertyBag != null)
        {
            result.AddDifference(new(
                PropertyName: GetPropertyName(stepName, nameof(TestStep.PropertyBag)),
                Value1: null,
                Value2: testStep2.PropertyBag,
                Type: DifferenceType.Added));
        }
        else if (testStep1.PropertyBag != null && testStep2.PropertyBag != null)
        {
            // Loop over the key-value pairs of the first dictionary
            foreach (PropertyBagKey key in testStep1.PropertyBag.Keys)
            {
                // Get the value
                IPropertyBagValue value1 = testStep1.PropertyBag.GetProperty(key);

                // Check if the second dictionary contains the same key
                if (!testStep2.PropertyBag.ContainsKey(key))
                {
                    // If not, then key has been removed from testStep2.PropertyBag
                    result.AddDifference(new Difference(
                        PropertyName: GetPropertyName(stepName, nameof(TestStep.PropertyBag), key.ToString()),
                        Value1: value1,
                        Value2: null,
                        Type: DifferenceType.Removed));
                }
                // Check if the second dictionary has the same value for the key
                else
                {
                    IPropertyBagValue value2 = testStep2.PropertyBag.GetProperty(key);

                    if (!value1.Equals(value2))
                    {
                        // If not, they are not equal
                        result.AddDifference(new Difference(
                            PropertyName: GetPropertyName(stepName, nameof(TestStep.PropertyBag), key.ToString()),
                            Value1: value1,
                            Value2: value2,
                            Type: DifferenceType.Changed));
                    }
                }
            }

            // Loop over the key-value pairs of the second dictionary
            foreach (PropertyBagKey key in testStep2.PropertyBag.Keys)
            {
                // Get the value
                IPropertyBagValue value2 = testStep2.PropertyBag.GetProperty(key);

                // Check if the first dictionary contains the same key
                if (!testStep1.PropertyBag.ContainsKey(key))
                {
                    // If not, then key has been added to testStep2.PropertyBag
                    result.AddDifference(new Difference(
                        PropertyName: GetPropertyName(stepName, nameof(TestStep.PropertyBag), key.ToString()),
                        Value1: null,
                        Value2: value2,
                        Type: DifferenceType.Added));
                }
            }
        }
    }
}
