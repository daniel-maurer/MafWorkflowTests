-- 1. Atualizar notas/highlights de Carlos Eduardo Silva
UPDATE customers
SET "Notes" = 'Cliente assíduo e focado em tecnologia e moda casual esportiva. Tem preferência por tênis Nike e periféricos ergonômicos. Já mencionou que joga nos finais de semana e usa o notebook para trabalho híbrido. Gosta de entrega rápida via Uber Flash/Sedex e prefere pagar via Pix ou parcelado sem juros.',
    "UpdatedAt" = now()
WHERE "Id" = 'ffaacde6-7f16-489f-9328-8127b54d5a87';

-- 2. Endereço adicional de trabalho para Carlos Eduardo Silva
INSERT INTO customer_addresses ("Id", "CustomerId", "Label", "Street", "Number", "Complement", "Neighborhood", "City", "State", "ZipCode", "Country", "IsDefault", "CreatedAt", "UpdatedAt")
VALUES ('b92b67e8-4660-4412-a720-33796d11f5a1', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'Escritório / Faria Lima', 'Av. Brigadeiro Faria Lima', '3477', '10º andar', 'Itaim Bibi', 'São Paulo', 'SP', '04538-133', 'BR', false, now(), now())
ON CONFLICT ("Id") DO NOTHING;

-- 3. Métodos de Entrega
INSERT INTO delivery_methods ("Id", "Name", "Type", "Description", "Price", "Active", "CreatedAt", "UpdatedAt")
VALUES 
  ('a1111111-1111-1111-1111-111111111111', 'Sedex Express', 'delivery', 'Envio rápido com código de rastreamento pelos Correios (1 a 2 dias úteis)', 24.90, true, now(), now()),
  ('a2222222-2222-2222-2222-222222222222', 'Uber Flash Express', 'delivery', 'Entrega no mesmo dia em até 2 horas na Grande São Paulo', 18.50, true, now(), now()),
  ('a3333333-3333-3333-3333-333333333333', 'Retirada na Loja Física', 'pickup', 'Disponível em até 30 minutos na nossa loja conceito na Av. Paulista', 0.00, true, now(), now())
ON CONFLICT ("Id") DO NOTHING;

-- 4. Dados da Loja Física
INSERT INTO store_info ("Id", "Name", "Address", "Phone", "Email", "CreatedAt", "UpdatedAt")
VALUES ('e1111111-1111-1111-1111-111111111111', 'MAF Concept Store', 'Av. Paulista, 1000 - Bela Vista, São Paulo - SP, CEP: 01310-100', '(11) 3214-5500', 'atendimento@mafstore.com.br', now(), now())
ON CONFLICT ("Id") DO NOTHING;

-- 5. Campanhas Promocionais
INSERT INTO campaigns (id, name, description, start_date, end_date, is_active, free_shipping, global_discount_percent, discount_1_item, discount_2_items, discount_3_plus_items, custom_rules_json, created_at)
VALUES 
  ('c1111111-1111-1111-1111-111111111111', 'Mês de Aniversário MAF', 'Descontos progressivos: 10% em 1 peça, 15% em 2 peças e 20% em 3+ peças, além de Frete Grátis para todo o Brasil!', now() - interval '2 days', now() + interval '28 days', true, true, 10.00, 10.00, 15.00, 20.00, NULL, now()),
  ('c2222222-2222-2222-2222-222222222222', 'Semana do Consumidor Tech', 'Condições especiais em tecnologia e periféricos com parcelamento em até 10x sem juros.', now() - interval '10 days', now() + interval '10 days', true, false, 5.00, 5.00, 10.00, 15.00, NULL, now())
ON CONFLICT (id) DO NOTHING;

-- 6. Pedidos de Carlos Eduardo Silva
-- Pedido 1: Sneaker Nike + Fone JBL
INSERT INTO orders ("Id", "CustomerId", "Status", "TotalAmount", "DeliveryMethod", "PaymentMethod", "CreatedAt", "UpdatedAt")
VALUES ('11111111-c421-419a-9e11-111111111111', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'Confirmed', 1099.80, 'Sedex Express', 'Cartão de Crédito 6x', now() - interval '2 days', now() - interval '2 days')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO order_items ("Id", "OrderId", "ProductId", "Sku", "Name", "Quantity", "UnitPrice", "TotalPrice")
VALUES 
  ('11111111-item-1111-1111-111111111111', '11111111-c421-419a-9e11-111111111111', 'd005da03-81ad-41bc-af83-646bc9f6af2f', 'NIKE-AM90-PT-42', 'Nike Air Max 90', 1, 899.90, 899.90),
  ('11111111-item-2222-2222-222222222222', '11111111-c421-419a-9e11-111111111111', '55a6d3bc-2296-4148-8b79-31c46865178d', 'FONE-JBL-T510', 'Fone JBL Tune 510BT', 1, 199.90, 199.90)
ON CONFLICT ("Id") DO NOTHING;

-- Pedido 2: Notebook Lenovo + Mouse Logitech
INSERT INTO orders ("Id", "CustomerId", "Status", "TotalAmount", "DeliveryMethod", "PaymentMethod", "CreatedAt", "UpdatedAt")
VALUES ('22222222-c421-419a-9e11-222222222222', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'Confirmed', 3578.90, 'Uber Flash Express', 'Pix à vista', now() - interval '5 days', now() - interval '5 days')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO order_items ("Id", "OrderId", "ProductId", "Sku", "Name", "Quantity", "UnitPrice", "TotalPrice")
VALUES 
  ('22222222-item-1111-1111-111111111111', '22222222-c421-419a-9e11-222222222222', '2dc5fdae-6c26-49fb-bb41-cd43cc205e01', 'NB-LENOVO-I5-16', 'Notebook Lenovo IdeaPad 3i', 1, 3499.00, 3499.00),
  ('22222222-item-2222-2222-222222222222', '22222222-c421-419a-9e11-222222222222', 'de45014d-e22e-443c-808f-a3455efbaadf', 'MOUS-LOG-WL', 'Mouse Logitech M280 Wireless', 1, 79.90, 79.90)
ON CONFLICT ("Id") DO NOTHING;

-- Pedido 3: Camisetas Polo Clássica
INSERT INTO orders ("Id", "CustomerId", "Status", "TotalAmount", "DeliveryMethod", "PaymentMethod", "CreatedAt", "UpdatedAt")
VALUES ('33333333-c421-419a-9e11-333333333333', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'Confirmed', 259.80, 'Retirada na Loja Física', 'Cartão de Débito', now() - interval '10 days', now() - interval '10 days')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO order_items ("Id", "OrderId", "ProductId", "Sku", "Name", "Quantity", "UnitPrice", "TotalPrice")
VALUES 
  ('33333333-item-1111-1111-111111111111', '33333333-c421-419a-9e11-333333333333', 'ba92c692-223b-4e01-b71e-d89ecdbc2963', 'CAM-POLO-AZ-M', 'Camiseta Polo Clássica', 2, 129.90, 259.80)
ON CONFLICT ("Id") DO NOTHING;

-- 7. Conversas de Carlos Eduardo Silva
-- Conversa 1: Compra de Notebook e Periférico
INSERT INTO conversations ("Id", "CustomerId", "SessionId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('44444444-c421-419a-9e11-444444444444', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'sess_carlos_note_work_20261001', 'closed', now() - interval '5 days', now() - interval '5 days')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO conversation_messages ("Id", "ConversationId", "Role", "Content", "CreatedAt")
VALUES 
  ('44444444-msg-1111-1111-111111111111', '44444444-c421-419a-9e11-444444444444', 'user', 'Olá! Preciso de um notebook bom e rápido para trabalho híbrido, que tenha pelo menos 16GB de RAM e processador potente.', now() - interval '5 days' + interval '1 minute'),
  ('44444444-msg-2222-2222-222222222222', '44444444-c421-419a-9e11-444444444444', 'assistant', 'Olá Carlos! Que bom ter você conosco. Temos exatamente o que você precisa: o Notebook Lenovo IdeaPad 3i com Intel Core i5 e 16GB de RAM por R$ 3.499,00. Excelente para multitarefas pesadas e muito portátil!', now() - interval '5 days' + interval '2 minutes'),
  ('44444444-msg-3333-3333-333333333333', '44444444-c421-419a-9e11-444444444444', 'user', 'Show de bola! Vocês têm algum mouse sem fio bom e compacto para levar junto na mochila?', now() - interval '5 days' + interval '3 minutes'),
  ('44444444-msg-4444-4444-444444444444', '44444444-c421-419a-9e11-444444444444', 'assistant', 'Temos sim! O Mouse Logitech M280 Wireless por R$ 79,90. Ele é super anatômico, silencioso e as pilhas duram até 18 meses. Combina perfeitamente com o IdeaPad 3i.', now() - interval '5 days' + interval '4 minutes'),
  ('44444444-msg-5555-5555-555555555555', '44444444-c421-419a-9e11-444444444444', 'user', 'Perfeito, vou querer os dois! Consegue mandar via Uber Flash aqui no meu escritório na Faria Lima?', now() - interval '5 days' + interval '5 minutes'),
  ('44444444-msg-6666-6666-666666666666', '44444444-c421-419a-9e11-444444444444', 'assistant', 'Com certeza Carlos! O Uber Flash sai por R$ 18,50 e chega em até 2 horas. Total final com desconto de 5% no Pix: R$ 3.578,90. Orçamento aprovado e pedido #22222222 gerado com sucesso!', now() - interval '5 days' + interval '6 minutes')
ON CONFLICT ("Id") DO NOTHING;

-- Conversa 2: Compra de Tênis e Fone Bluetooth
INSERT INTO conversations ("Id", "CustomerId", "SessionId", "Status", "CreatedAt", "UpdatedAt")
VALUES ('55555555-c421-419a-9e11-555555555555', 'ffaacde6-7f16-489f-9328-8127b54d5a87', 'sess_carlos_sneakers_20261004', 'closed', now() - interval '2 days', now() - interval '2 days')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO conversation_messages ("Id", "ConversationId", "Role", "Content", "CreatedAt")
VALUES 
  ('55555555-msg-1111-1111-111111111111', '55555555-c421-419a-9e11-555555555555', 'user', 'E aí! Tô procurando um tênis estiloso e confortável pra caminhadas e dia a dia.', now() - interval '2 days' + interval '1 minute'),
  ('55555555-msg-2222-2222-222222222222', '55555555-c421-419a-9e11-555555555555', 'assistant', 'Fala Carlos! A melhor recomendação é o Nike Air Max 90 Preto no tamanho 42 por R$ 899,90. Ele é um clássico atemporal com amortecimento a ar imbatível.', now() - interval '2 days' + interval '2 minutes'),
  ('55555555-msg-3333-3333-333333333333', '55555555-c421-419a-9e11-555555555555', 'user', 'Massa! Tem algum fone bluetooth pra ouvir música durante as caminhadas?', now() - interval '2 days' + interval '3 minutes'),
  ('55555555-msg-4444-4444-444444444444', '55555555-c421-419a-9e11-555555555555', 'assistant', 'Temos o Fone JBL Tune 510BT por R$ 199,90. É sem fio, dobrável, leve e tem bateria pra 40 horas com som Pure Bass!', now() - interval '2 days' + interval '4 minutes'),
  ('55555555-msg-5555-5555-555555555555', '55555555-c421-419a-9e11-555555555555', 'user', 'Fechado, vou querer os dois. Envia por Sedex pro meu apê na Paulista e parcela em 6x no cartão.', now() - interval '2 days' + interval '5 minutes'),
  ('55555555-msg-6666-6666-666666666666', '55555555-c421-419a-9e11-555555555555', 'assistant', 'Maravilha Carlos! Pedido #11111111 concluído no valor de R$ 1.099,80 em 6x de R$ 183,30 sem juros via Sedex Express. Seu código de rastreio será enviado por e-mail!', now() - interval '2 days' + interval '6 minutes')
ON CONFLICT ("Id") DO NOTHING;
