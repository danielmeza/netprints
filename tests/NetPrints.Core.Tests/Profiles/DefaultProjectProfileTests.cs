using NetPrints.Core;
using Xunit;

namespace NetPrints.Tests.Profiles
{
    /// <summary>
    /// <see cref="DefaultProjectProfile"/> (extension-points.md §5, data-model.md §5): the built-in
    /// profile's shape, and its one class template's factory.
    /// </summary>
    public class DefaultProjectProfileTests
    {
        [Fact]
        public void InstanceIsASingletonWithTheExpectedIdAndBaseType()
        {
            IProjectProfile profile = DefaultProjectProfile.Instance;

            Assert.Same(DefaultProjectProfile.Instance, profile);
            Assert.Equal("netprints.default", profile.Id);
            Assert.Equal(DefaultProjectProfile.ProfileId, profile.Id);
            Assert.Equal(TypeSpecifier.FromType<object>(), Assert.Single(profile.BaseTypes));
            Assert.Null(profile.CatalogProfileId);
        }

        [Fact]
        public void ProjectTemplateHasEveryPlaceholderApartFromProjectName()
        {
            string template = DefaultProjectProfile.Instance.ProjectTemplate;

            Assert.Contains("{TargetFramework}", template, System.StringComparison.Ordinal);
            Assert.Contains("{RootNamespace}", template, System.StringComparison.Ordinal);
            Assert.Contains("{ProfileId}", template, System.StringComparison.Ordinal);
            Assert.Contains("{NetPrintsSdkVersion}", template, System.StringComparison.Ordinal);
        }

        [Fact]
        public void EmptyClassTemplateCreatesAPublicClassInTheProjectsDefaultNamespace()
        {
            Project project = Project.CreateNew("MyProject", "MyProject.Namespace", addDefaultReferences: false);
            ClassTemplate template = Assert.Single(DefaultProjectProfile.Instance.ClassTemplates,
                t => t.Id == DefaultProjectProfile.EmptyClassTemplateId);

            ClassGraph created = template.Create(project, "MyClass");

            Assert.Equal("MyClass", created.Name);
            Assert.Equal(project.DefaultNamespace, created.Namespace);
            Assert.Equal(MemberVisibility.Public, created.Visibility);
            Assert.Same(project, created.Project);
            Assert.Empty(created.Methods);
            Assert.Empty(created.Variables);
            Assert.Empty(created.Constructors);
        }
    }
}
