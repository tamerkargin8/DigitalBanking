# Yerel geliştirme

Proje .NET 10 kullanır. Bu Mac için SDK: ~/Documents/Codex/toolchains/dotnet (10.0.402, ARM64). scripts/dotnet.sh sistemde dotnet varsa onu, yoksa bu SDK'yı kullanır.

VS Code: Terminal > Run Build Task ile derleyin. Terminal > Run Task > DigitalBanking: Run local ile API ve dashboard'u birlikte başlatın. Görevi durdurmak için terminalde Ctrl+C kullanın.

Terminal alternatifi:

```sh
bash scripts/dotnet.sh build DigitalBanking.slnx
python3 scripts/run_local.py
```

Dashboard: http://127.0.0.1:5081 ; API/Swagger: http://127.0.0.1:5080/swagger ; health: http://127.0.0.1:5080/health .

İlk açılışta rastgele demo şifresi ve JWT anahtarı .local/credentials.json içine yalnızca kullanıcı tarafından okunabilir şekilde kaydedilir. admin ve tamer aynı yerel demo şifresini kullanır. Bu dosyayı paylaşmayın. Giriş ekranında admin ve dosyadaki password değerini kullanın.

SQLite veritabanı .local/digitalbanking.db içindedir. Yalnızca Development içindir. SQL Server varsayılan sağlayıcı olmaya devam eder; mevcut migration dosyaları değiştirilmez. SQLite EnsureCreated kullanır, SQL Server migration'larını uygulamaz. Şema değişirse mevcut demo dosyasını arşivleyip yeni demo veritabanı oluşturun. SQLite ile yapılan kontroller SQL Server doğrulamasının yerine geçmez.

Gizli anahtarlar ortam değişkenlerinden geçirilir. Production'da otomatik migration/demo kullanıcı oluşturma kapalıdır; SQL Server migration'ları kontrollü uygulanmalı ve Jwt__SecretKey ayrı sağlanmalıdır.

```sh
python3 scripts/smoke_test.py
```

Bu kontrol çalışan yerel API'ye sentetik müşteri/hesap/işlem ekler. Gerçek müşteri bilgisi kullanmayın.

DeepSeek kopyasında aynı komutları farklı portlarla çalıştırın:

```sh
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```
