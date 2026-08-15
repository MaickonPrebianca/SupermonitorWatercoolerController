# Supermonitor Watercooler Controller para Windows
Aplicativo de monitoramento de informações de hardware em C# / WPF projetado para ler dados de sensores do sistema (CPU e GPU) e enviá-los via comunicação Serial (COM) para o display LCD do watercooler compatível e testado com o **Superframe Isengard Smart** no Sistema Operacional Microsoft Windows 10. 

Versão disponível para Linux no link: https://github.com/MaickonPrebianca/super-monitor-ch340

---

## 📷 Demonstração

| Interface do Aplicativo | Dispositivo em Execução |
| :---: | :---: |
| ![Interface do App](docs/images/appScreen.png) | ![Watercooler LCD](docs/images/imagemWatercooler.png) |

---

## 🚀 Funcionalidades

- **Monitoramento em Tempo Real:** Leitura contínua de Temperatura, Carga (Load) e Rotação (RPM) de CPU e GPU via biblioteca `LibreHardwareMonitor`.
- **Mapeamento Dinâmico de Sensores:** Mapeamento customizado de cada barra de progresso diretamente pelos menus seletores na interface.
- **Cálculo Dinâmico da Bomba:** Opção de calcular a velocidade da bomba automaticamente (ex: 1,6x a rotação do fan) ou mapear o sensor físico da placa-mãe.
- **Persistência de Configurações:** Salva automaticamente as preferências de sensores, portas COM e opções de inicialização em formato JSON.
- **Minimizar para System Tray (Bandeja):** Execução contínua em segundo plano no menu do sistema.
- **Bypass de UAC Automático:** Criação automática de tarefa no Agendador do Windows com privilégios elevados na primeira execução, permitindo inicialização sem avisos repetitivos do Windows.

---

## 🛠️ Dependências

- **Runtime:** [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Bibliotecas C# / NuGet:**
  - `LibreHardwareMonitorLib` (0.9.3 ou superior) https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/master/LibreHardwareMonitorLib
  - `System.IO.Ports`
  - `System.Text.Json`

---

## 📦 Instalação e Execução

### Opção 1: Executável Direto (Recomendado)
1. Baixe a última versão na seção [Releases](../../releases).
2. Extraia os arquivos e execute o `AIOController.exe`.
3. **Importante:** O aplicativo **deve ser executado como Administrador** na primeira vez para que a biblioteca de hardware possa acessar os sensores do sistema e para registrar a tarefa no Agendador do Windows.

### Opção 2: Compilando do Código Fonte
1. Clone o repositório:
   ```bash
   git clone [https://github.com/seu-usuario/AIOController.git](https://github.com/seu-usuario/AIOController.git)

---

## ⚠️ AVISOS E TERMOS IMPORTANTE DE HARDWARE

> 💡 **Nota sobre o indicador PUMP Speed:**
> O dado de velocidade da bomba (**PUMP Speed**) exibido na tela do software e enviado ao display é **baseado matematicamente no Fan Speed** e **NÃO reflete a velocidade real física da bomba de refrigeração**.
> 
> Caso queira que o valor reflita a rotação física real da bomba, é necessário conectar o sensor físico da bomba ao conector **PUMP_FAN / PUMP_IO** dedicado da sua placa-mãe e realizar a seleção do respectivo sensor lido na tela do app.

---

## 📄 Licença e Termos Legais

Este projeto está licenciado sob os termos da licença [MIT](LICENSE).

### Termos de Isenção de Responsabilidade (Disclaimer)
1. **Ausência de Vínculo Comercial:** Embora o aplicativo seja compatível e testado no modelo *SuperFrame Smart 240mm*, o autor/desenvolvedor **não possui qualquer vínculo, associação, patrocínio ou responsabilidade** com a marca fabricante ou distribuidora do dispositivo.
2. **Propriedade Intelectual e Marcas:** Quaisquer marcas, nomes de produtos, logotipos ou modelos citados nesta documentação e código pertencem exclusivamente aos seus respectivos proprietários e são aqui utilizados **apenas para fins informativos e de identificação de compatibilidade**.
3. **Leitura Segura:** O software realiza **apenas a leitura** dos sensores do sistema e envio de dados para exibição visual. Ele **não altera** o funcionamento, perfis de ventoinha ou voltagens da BIOS/hardware.
4. **Isenção de Danos:** O software é fornecido "COMO ESTÁ" (*AS IS*), sem garantias de qualquer tipo. O uso, modificação ou redistribuição do código é por sua conta e risco.

---

## 🤝 Apoie o Projeto

Se este software foi útil para você, considere apoiar o desenvolvimento voluntário.

