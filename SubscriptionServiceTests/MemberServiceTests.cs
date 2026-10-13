using Moq;
using Subscription_Service.Services.Interfaces;
using Subscription_Service.Models;
using Subscription_Service.Services;

namespace SubscriptionServiceTests
{
    public class MemberServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();

        private readonly MemberService _service;

        public MemberServiceTests()
        {
            _service = new MemberService(_repo.Object);
        }

        /// <summary>Перевірка на null: Якщо користувача з таким ID немає в базі, повертається null.</summary>
        [Fact]
        public void GetMember_WhenNotFound_ReturnsNull()
        {
            // Arrange
            _repo.Setup(x => x.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var result = _service.GetMember(99);

            // Assert
            Assert.Null(result);
        }

        /// <summary>Перевірка точного значення: Метод повертає правильного користувача, якщо він існує.</summary>
        [Fact]
        public void GetMember_WhenFound_ReturnsMember()
        {
            // Arrange
            var expectedMember = new Member { Id = 1, Name = "Name_X" };
            _repo.Setup(x => x.GetById(1)).Returns(expectedMember);

            // Act
            var result = _service.GetMember(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedMember.Id, result.Id);
            Assert.Equal("Name_X", result.Name);
        }

        /// <summary>Будь-яка булева перевірка: Метод IsActive повертає true для активного користувача.</summary>
        [Fact]
        public void IsActive_WhenUserIsActive_ReturnsTrue()
        {
            // Arrange
            _repo.Setup(x => x.GetById(2)).Returns(new Member { Id = 2, IsActive = true });

            // Act
            var isActive = _service.IsActive(2);

            // Assert
            Assert.True(isActive);
        }

        /// <summary>Протилежна булева перевірка: Метод IsActive повертає false для неактивного користувача.</summary>
        [Fact]
        public void IsActive_WhenUserIsNotActive_ReturnsFalse()
        {
            // Arrange
            _repo.Setup(x => x.GetById(3)).Returns(new Member { Id = 3, IsActive = false });

            // Act
            var isActive = _service.IsActive(3);

            // Assert
            Assert.False(isActive);
        }

        /// <summary>Дозволяє приймати будь-яке значення аргументу: Метод IsActive повертає false, якщо користувача взагалі не знайдено.</summary>
        [Fact]
        public void IsActive_WhenUserNotFound_ReturnsFalse()
        {
            // Arrange
            _repo.Setup(x => x.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act
            var isActive = _service.IsActive(99);

            // Assert
            Assert.False(isActive);
        }
    }
}
