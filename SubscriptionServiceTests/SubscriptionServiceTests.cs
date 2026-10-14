using Moq;
using Subscription_Service.Models;
using Subscription_Service.Services;
using Subscription_Service.Services.Interfaces;

namespace SubscriptionServiceTests
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<IMemberRepository> _repo = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<INotificationService> _notify = new();

        private readonly SubscriptionService _service;

        public SubscriptionServiceTests()
        {
            _service = new SubscriptionService(_repo.Object, _payment.Object, _notify.Object);
        }

        /// <summary>Перевірка винятків: Виклик методу з некоректними даними (користувач не знайдений).</summary>
        [Fact]
        public void RenewSubscription_WhenMemberNotFound_ThrowsArgumentException()
        {
            // Arrange
            _repo.Setup(x => x.GetById(It.IsAny<int>())).Returns((Member?)null);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.RenewSubscription(1, 100m, 30));
        }

        /// <summary>Переконатись, що метод не викликано: Якщо платіж відхилено, підписка не оновлюється.</summary>
        [Fact]
        public void RenewSubscription_WhenPaymentFails_ReturnsFalseAndDoesNotUpdate()
        {
            // Arrange
            var member = new Member { Id = 1, IsActive = false };
            _repo.Setup(x => x.GetById(1)).Returns(member);
            _payment.Setup(x => x.VerifyPayment(1, It.IsAny<decimal>())).Returns(false);

            // Act
            var result = _service.RenewSubscription(1, 50m, 30);

            // Assert
            Assert.False(result);
            _repo.Verify(x => x.Update(It.IsAny<Member>()), Times.Never);
            _notify.Verify(x => x.SendNotification(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>Параметризований тест із різними наборами даних: Успішне подовження підписки.</summary>
        [Theory]
        [InlineData(100, 30)]
        [InlineData(500, 365)]
        public void RenewSubscription_WhenSuccessful_UpdatesMemberAndSendsNotification(decimal paymentAmount, int days)
        {
            // Arrange
            var oldDate = DateTime.Now.AddDays(-1);
            var member = new Member { Id = 1, SubscriptionEnd = oldDate, IsActive = false };
            
            _repo.Setup(x => x.GetById(1)).Returns(member);
            _payment.Setup(x => x.VerifyPayment(1, paymentAmount)).Returns(true);

            var beforeRenewal = DateTime.Now;

            // Act        
            var result = _service.RenewSubscription(1, paymentAmount, days);

            var afterRenewal = DateTime.Now;

            // Assert
            Assert.True(result);
            Assert.NotEqual(oldDate, member.SubscriptionEnd); 
            Assert.InRange(
                member.SubscriptionEnd!.Value, 
                beforeRenewal.AddDays(days), 
                afterRenewal.AddDays(days)
            );

            _repo.Verify(x => x.Update(It.Is<Member>(m => m.IsActive == true)), Times.Exactly(1));
            _notify.Verify(x => x.SendNotification("Subscription renewed!", 1));
        }

        /// <summary>Перевірка порожності та колекції: Деактивація лише прострочених акаунтів (з перехопленням результату).</summary>
        [Fact]
        public void DeactivateExpiredMembers_OnlyDeactivatesExpiredOnes()
        {
            // Arrange
            var expiredMember = new Member { Id = 1, SubscriptionEnd = DateTime.Now.AddDays(-5), IsActive = true };
            var activeMember = new Member { Id = 2, SubscriptionEnd = DateTime.Now.AddDays(5), IsActive = true };
            var membersList = new List<Member> { expiredMember, activeMember };

            _repo.Setup(x => x.GetAll()).Returns(membersList);

            var updatedMembers = new List<Member>();
            _repo.Setup(x => x.Update(It.IsAny<Member>()))
                 .Callback<Member>(m => updatedMembers.Add(m));

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.NotEmpty(updatedMembers); 
            Assert.Contains(expiredMember, updatedMembers); 
            Assert.False(expiredMember.IsActive);
            Assert.True(activeMember.IsActive);
            
            _repo.Verify(x => x.GetAll(), Times.Once);
            _repo.Verify(x => x.Update(It.Is<Member>(m => m.Id == 1)), Times.Once);
            _repo.Verify(x => x.Update(It.Is<Member>(m => m.Id == 2)), Times.Never);

            _notify.Verify(x => x.SendNotification("Membership expired", 1), Times.Once);
            _notify.Verify(x => x.SendNotification(It.IsAny<string>(), 2), Times.Never);
        }

        /// <summary>Перевірка на null: акаунти з безлімітною підпискою (SubscriptionEnd = null) не блокуються.</summary>
        [Fact]
        public void DeactivateExpiredMembers_WithLifetimeSubscription_DoesNotDeactivate()
        {
            // Arrange
            var lifetimeMember = new Member { Id = 3, SubscriptionEnd = null, IsActive = true };
            var membersList = new List<Member> { lifetimeMember };
            
            _repo.Setup(x => x.GetAll()).Returns(membersList);

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.True(lifetimeMember.IsActive); 
            _repo.Verify(x => x.Update(It.Is<Member>(m => m.Id == 3)), Times.Never);
        }

        /// <summary>Перевірка порожності: Метод коректно обробляє порожню базу даних без винятків.</summary>
        [Fact]
        public void DeactivateExpiredMembers_WhenDatabaseIsEmpty_DoesNothing()
        {
            // Arrange
            var emptyMembersList = new List<Member>();
            _repo.Setup(x => x.GetAll()).Returns(emptyMembersList);

            var updatedMembers = new List<Member>();
            _repo.Setup(x => x.Update(It.IsAny<Member>()))
                 .Callback<Member>(m => updatedMembers.Add(m));

            // Act
            _service.DeactivateExpiredMembers();

            // Assert
            Assert.Empty(updatedMembers); // Безпосередньо перевіряємо, що нікого не передали на оновлення
            _repo.Verify(x => x.GetAll(), Times.Once);
            _repo.Verify(x => x.Update(It.IsAny<Member>()), Times.Never);
        }
    }

}
