-- Inserir itens para os pedidos criados
INSERT INTO order_items ("Id", "OrderId", "ProductId", "Sku", "Name", "Quantity", "UnitPrice", "TotalPrice")
VALUES 
  (gen_random_uuid(), '11111111-c421-419a-9e11-111111111111', 'd005da03-81ad-41bc-af83-646bc9f6af2f', 'NIKE-AM90-PT-42', 'Nike Air Max 90', 1, 899.90, 899.90),
  (gen_random_uuid(), '11111111-c421-419a-9e11-111111111111', '55a6d3bc-2296-4148-8b79-31c46865178d', 'FONE-JBL-T510', 'Fone JBL Tune 510BT', 1, 199.90, 199.90),
  (gen_random_uuid(), '22222222-c421-419a-9e11-222222222222', '2dc5fdae-6c26-49fb-bb41-cd43cc205e01', 'NB-LENOVO-I5-16', 'Notebook Lenovo IdeaPad 3i', 1, 3499.00, 3499.00),
  (gen_random_uuid(), '22222222-c421-419a-9e11-222222222222', 'de45014d-e22e-443c-808f-a3455efbaadf', 'MOUS-LOG-WL', 'Mouse Logitech M280 Wireless', 1, 79.90, 79.90),
  (gen_random_uuid(), '33333333-c421-419a-9e11-333333333333', 'ba92c692-223b-4e01-b71e-d89ecdbc2963', 'CAM-POLO-AZ-M', 'Camiseta Polo Clássica', 2, 129.90, 259.80);

-- Inserir mensagens para a conversa 1 (Notebook e Mouse)
INSERT INTO conversation_messages ("Id", "ConversationId", "Role", "Content", "CreatedAt")
VALUES 
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'user', 'Olá! Preciso de um notebook bom e rápido para trabalho híbrido, que tenha pelo menos 16GB de RAM e processador potente.', now() - interval '5 days' + interval '1 minute'),
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'assistant', 'Olá Carlos! Que bom ter você conosco. Temos exatamente o que você precisa: o Notebook Lenovo IdeaPad 3i com Intel Core i5 e 16GB de RAM por R$ 3.499,00. Excelente para multitarefas pesadas e muito portátil!', now() - interval '5 days' + interval '2 minutes'),
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'user', 'Show de bola! Vocês têm algum mouse sem fio bom e compacto para levar junto na mochila?', now() - interval '5 days' + interval '3 minutes'),
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'assistant', 'Temos sim! O Mouse Logitech M280 Wireless por R$ 79,90. Ele é super anatômico, silencioso e as pilhas duram até 18 meses. Combina perfeitamente com o IdeaPad 3i.', now() - interval '5 days' + interval '4 minutes'),
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'user', 'Perfeito, vou querer os dois! Consegue mandar via Uber Flash aqui no meu escritório na Faria Lima?', now() - interval '5 days' + interval '5 minutes'),
  (gen_random_uuid(), '44444444-c421-419a-9e11-444444444444', 'assistant', 'Com certeza Carlos! O Uber Flash sai por R$ 18,50 e chega em até 2 horas. Total final com desconto de 5% no Pix: R$ 3.578,90. Orçamento aprovado e pedido #22222222 gerado com sucesso!', now() - interval '5 days' + interval '6 minutes');

-- Inserir mensagens para a conversa 2 (Tênis e Fone)
INSERT INTO conversation_messages ("Id", "ConversationId", "Role", "Content", "CreatedAt")
VALUES 
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'user', 'E aí! Tô procurando um tênis estiloso e confortável pra caminhadas e dia a dia.', now() - interval '2 days' + interval '1 minute'),
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'assistant', 'Fala Carlos! A melhor recomendação é o Nike Air Max 90 Preto no tamanho 42 por R$ 899,90. Ele é um clássico atemporal com amortecimento a ar imbatível.', now() - interval '2 days' + interval '2 minutes'),
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'user', 'Massa! Tem algum fone bluetooth pra ouvir música durante as caminhadas?', now() - interval '2 days' + interval '3 minutes'),
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'assistant', 'Temos o Fone JBL Tune 510BT por R$ 199,90. É sem fio, dobrável, leve e tem bateria pra 40 horas com som Pure Bass!', now() - interval '2 days' + interval '4 minutes'),
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'user', 'Fechado, vou querer os dois. Envia por Sedex pro meu apê na Paulista e parcela em 6x no cartão.', now() - interval '2 days' + interval '5 minutes'),
  (gen_random_uuid(), '55555555-c421-419a-9e11-555555555555', 'assistant', 'Maravilha Carlos! Pedido #11111111 concluído no valor de R$ 1.099,80 em 6x de R$ 183,30 sem juros via Sedex Express. Seu código de rastreio será enviado por e-mail!', now() - interval '2 days' + interval '6 minutes');
